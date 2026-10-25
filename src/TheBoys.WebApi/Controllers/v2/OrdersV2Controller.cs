using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Application.Orders;
using TheBoys.Application.Orders.Commands.CancelOrderV2;
using TheBoys.Application.Orders.Commands.CheckoutQuote;
using TheBoys.Application.Orders.Commands.ReviewCancellationRequest;
using TheBoys.Application.Payments.Commands.CreatePaymentAttempt;
using TheBoys.Application.Payments.Commands.CreateRefund;
using TheBoys.Application.Payments.DTOs;
using TheBoys.Application.Payments.Queries.GetOrderRefunds;
using TheBoys.Application.Quotes;
using TheBoys.Domain.Entities;
using TheBoys.WebApi.Security;

namespace TheBoys.WebApi.Controllers.v2;

public sealed class CheckoutQuoteRequest
{
    public Guid QuoteId { get; set; }
}

public sealed class CreatePaymentAttemptRequest
{
    public string? Note { get; set; }
}

public sealed class CancelOrderV2ApiRequest
{
    public string Reason { get; set; } = default!;
    public string? DecisionNote { get; set; }
}

public sealed class ReviewCancellationRequestApiRequest
{
    public bool Approved { get; set; }
    public string? DecisionNote { get; set; }
}

public sealed class CreateRefundApiRequest
{
    public decimal? AmountVnd { get; set; }
    public string? Reason { get; set; }
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

        var isManagement = IsManagement;
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

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(
        Guid id,
        [FromBody] CancelOrderV2ApiRequest request,
        CancellationToken ct)
    {
        var command = new CancelOrderV2Command(id, UserId, request.Reason, IsManagement, request.DecisionNote);
        var result = await mediator.Send(command, ct);

        if (result.IsFailure)
        {
            return ProblemResult(result.Error);
        }

        return Ok(result.Value);
    }

    [HttpPost("cancellation-requests/{requestId:guid}/review")]
    [Authorize(Roles = Roles.Management)]
    public async Task<IActionResult> ReviewCancellationRequest(
        Guid requestId,
        [FromBody] ReviewCancellationRequestApiRequest request,
        CancellationToken ct)
    {
        var command = new ReviewCancellationRequestCommand(
            RequestId: requestId,
            DecidedByUserId: UserId,
            Approved: request.Approved,
            DecisionNote: request.DecisionNote
        );

        var result = await mediator.Send(command, ct);
        if (result.IsFailure)
        {
            return ProblemResult(result.Error);
        }

        return Ok(result.Value);
    }

    [HttpPost("{id:guid}/refunds")]
    [Authorize(Roles = Roles.Management)]
    public async Task<IActionResult> CreateRefund(
        Guid id,
        [FromBody] CreateRefundApiRequest? request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct)
    {
        if (User.IsInRole(Roles.Manager))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey) || !Guid.TryParse(idempotencyKey, out var operationKey))
        {
            return BadRequest(new { message = "Header Idempotency-Key là bắt buộc và phải là UUID hợp lệ." });
        }

        var command = new CreateRefundCommand(
            OrderId: id,
            RequestedByUserId: UserId,
            OperationKey: operationKey,
            AmountVnd: request?.AmountVnd,
            Reason: request?.Reason ?? "Khách hàng / Quản trị viên yêu cầu hoàn tiền",
            IsManagement: IsManagement
        );

        var result = await mediator.Send(command, ct);
        if (result.IsFailure)
        {
            return ProblemResult(result.Error);
        }

        var (refund, isReplay) = result.Value;
        return isReplay ? Ok(refund) : StatusCode(StatusCodes.Status201Created, refund);
    }

    [HttpGet("{id:guid}/refunds")]
    public async Task<IActionResult> GetRefunds(Guid id, CancellationToken ct)
    {
        var query = new GetOrderRefundsQuery(id, UserId, IsManagement);
        var result = await mediator.Send(query, ct);

        if (result.IsFailure)
        {
            return ProblemResult(result.Error);
        }

        return Ok(result.Value);
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

