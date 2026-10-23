using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Application.Orders;
using TheBoys.Application.Orders.Commands.CheckoutQuote;
using TheBoys.Application.Payments.Commands.CreatePaymentAttempt;
using TheBoys.Application.Payments.DTOs;
using TheBoys.Application.Quotes;
using TheBoys.Domain.Entities;

namespace TheBoys.WebApi.Controllers.v2;

public sealed class CheckoutQuoteRequest
{
    public Guid QuoteId { get; set; }
}

public sealed class CreatePaymentAttemptRequest
{
    public string? Note { get; set; }
}

[ApiController, Route("api/v2/orders"), Authorize]
public sealed class OrdersV2Controller(ISender mediator, IApplicationDbContext db) : ApiControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    [HttpPost]
    public async Task<IActionResult> Checkout(
        [FromBody] CheckoutQuoteRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || !Guid.TryParse(idempotencyKey, out var clientRequestGuid))
        {
            return BadRequest(new { message = "Header Idempotency-Key là bắt buộc và phải là UUID hợp lệ." });
        }

        if (request.QuoteId == Guid.Empty)
        {
            return BadRequest(new { message = "QuoteId không hợp lệ." });
        }

        var key = clientRequestGuid.ToString("N");
        var fingerprintPayload = $"{request.QuoteId:N}:{UserId:N}";
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintPayload)));

        var command = new CheckoutQuoteCommand(UserId, request.QuoteId, key, fingerprint);
        var result = await mediator.Send(command, ct);

        if (result.IsFailure)
        {
            return ProblemResult(result.Error);
        }

        var (order, isReplay) = result.Value;
        var dto = MapToDto(order);

        return isReplay
            ? Ok(dto)
            : CreatedAtAction(nameof(Get), new { id = order.Id }, dto);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var order = await db.Orders
            .Include(o => o.Items)
            .Include(o => o.StatusHistories)
            .Include(o => o.PricingPackage)
            .AsNoTracking()
            .SingleOrDefaultAsync(o => o.Id == id, ct);

        if (order == null) return NotFound(new { message = "Không tìm thấy đơn hàng." });
        if (!CanAccess(order.UserId)) return Forbid();

        return Ok(MapToDto(order));
    }

    [HttpPost("{id:guid}/payment-attempts")]
    public async Task<IActionResult> CreatePaymentAttempt(
        Guid id,
        [FromBody] CreatePaymentAttemptRequest? request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || !Guid.TryParse(idempotencyKey, out var clientRequestGuid))
        {
            return BadRequest(new { message = "Header Idempotency-Key là bắt buộc và phải là UUID hợp lệ." });
        }

        var key = clientRequestGuid.ToString("N");
        var note = request?.Note ?? "";
        var fingerprintPayload = $"{id:N}:{UserId:N}:{note}";
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintPayload)));

        var isManagement = User.IsInRole("staff") || User.IsInRole("admin");
        var command = new CreatePaymentAttemptCommand(UserId, id, key, fingerprint, isManagement);
        var result = await mediator.Send(command, ct);

        if (result.IsFailure)
        {
            return ProblemResult(result.Error);
        }

        var (attempt, isReplay) = result.Value;
        return isReplay ? Ok(attempt) : StatusCode(StatusCodes.Status201Created, attempt);
    }

    [HttpGet("{id:guid}/payment-attempts")]
    public async Task<IActionResult> GetPaymentAttempts(Guid id, CancellationToken ct)
    {
        var order = await db.Orders
            .AsNoTracking()
            .SingleOrDefaultAsync(o => o.Id == id, ct);

        if (order == null) return NotFound(new { message = "Không tìm thấy đơn hàng." });
        if (!CanAccess(order.UserId)) return Forbid();

        var attempts = await db.PaymentAttempts
            .Where(pa => pa.OrderId == id)
            .OrderByDescending(pa => pa.CreatedAtUtc)
            .AsNoTracking()
            .ToListAsync(ct);

        var dtos = attempts.Select(CreatePaymentAttemptCommandHandler.MapToDto).ToList();
        return Ok(dtos);
    }

    private static OrderV2Dto MapToDto(Order o)
    {
        ShippingAddressDto address;
        if (!string.IsNullOrWhiteSpace(o.AddressSnapshotJson))
        {
            address = JsonSerializer.Deserialize<ShippingAddressDto>(o.AddressSnapshotJson, JsonOptions) ?? new ShippingAddressDto();
        }
        else
        {
            address = new ShippingAddressDto
            {
                ReceiverName = o.ReceiverName,
                ReceiverPhone = o.ReceiverPhone,
                StreetAddress = o.StreetAddress,
                District = o.District,
                City = o.City,
                CustomerNote = o.CustomerNote
            };
        }

        var items = o.Items.Select(i => new OrderItemDto(
            i.Id,
            i.LineNumber,
            i.VersionId,
            Convert.ToHexString(i.ConfigurationHash).ToLowerInvariant(),
            i.Quantity,
            i.UnitPriceVnd,
            i.MaterialSnapshot,
            i.HeightMm,
            i.EngravingText,
            i.PartnerUnitBudgetVnd
        )).ToList();

        var histories = o.StatusHistories.Select(h => new OrderStatusHistoryDto(
            h.Id,
            h.Axis,
            h.FromStatus,
            h.ToStatus,
            h.ActorUserId,
            h.Source,
            h.Reason,
            h.OperationKey,
            h.CreatedAtUtc
        )).ToList();

        return new OrderV2Dto(
            o.Id,
            o.OrderCode,
            o.UserId,
            o.QuoteId ?? Guid.Empty,
            o.FulfillmentStatus ?? "PendingPayment",
            o.OrderPaymentStatus ?? "Unpaid",
            o.Quantity,
            o.UnitPriceSnapshot,
            o.SubTotalVnd,
            o.FinalShippingFee,
            o.TotalAmount,
            o.Currency ?? "VND",
            address,
            o.MaterialSnapshot,
            o.HeightMmSnapshot,
            o.CreatedAtUtc,
            items,
            histories
        );
    }
}

