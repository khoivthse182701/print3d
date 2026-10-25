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
using TheBoys.Application.Common.Payments;
using System.Security.Claims;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;
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

public sealed class TopupPaymentRequest
{
    public string PackageId { get; set; } = default!;
}

public sealed class TopupPaymentResponse
{
    public bool Success { get; set; }
    public string PackageId { get; set; } = default!;
    public decimal AmountVnd { get; set; }
    public int Credits { get; set; }
    public string MerchantReference { get; set; } = default!;
    public string? CheckoutUrl { get; set; }
    public string? QrCode { get; set; }
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

    [HttpPost("/api/v2/payments/topup")]
    public async Task<IActionResult> Topup(
        [FromBody] TopupPaymentRequest request,
        [FromServices] IPaymentGatewayRegistry gatewayRegistry,
        [FromServices] IClock clock,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request?.PackageId))
            return BadRequest(new { message = "Gói nạp packageId là bắt buộc (starter, creator, studio)." });

        var pkg = request.PackageId.Trim().ToLowerInvariant();
        decimal amountVnd;
        int credits;

        switch (pkg)
        {
            case "starter":
                amountVnd = 50000m;
                credits = 3;
                break;
            case "creator":
                amountVnd = 120000m;
                credits = 10;
                break;
            case "studio":
                amountVnd = 290000m;
                credits = 30;
                break;
            default:
                return BadRequest(new { message = "Gói nạp không hợp lệ. Chọn starter, creator hoặc studio." });
        }

        IPaymentGateway gateway;
        try
        {
            gateway = gatewayRegistry.ResolveGateway("PayOS");
            if (!gateway.IsConfigured)
            {
                gateway = gatewayRegistry.ResolveActiveGateway();
            }
        }
        catch
        {
            gateway = gatewayRegistry.ResolveActiveGateway();
        }

        var now = clock.UtcNow;
        var merchantReference = $"TOPUP_{pkg}_{UserId}_{Guid.NewGuid():N}";

        var package = await db.PricingPackages.FirstOrDefaultAsync(ct);
        if (package == null)
        {
            package = new PricingPackage
            {
                Id = Guid.NewGuid(),
                PackageCode = "TIER_TOPUP_AUTO",
                Name = "Topup Auto Package",
                Material = "PLA",
                PriceVnd = amountVnd,
                PartnerBaseCostVnd = 0,
                IsActive = true,
                CreatedAtUtc = now
            };
            db.PricingPackages.Add(package);
            await db.SaveChangesAsync(ct);
        }

        var topupOrder = new Order
        {
            Id = Guid.NewGuid(),
            OrderCode = $"TOPUP-{now:yyyyMMdd}-{Guid.NewGuid():N}"[..30],
            UserId = UserId,
            PricingPackageId = package.Id,
            Status = OrderStatus.Draft,
            FulfillmentStatus = "PendingPayment",
            OrderPaymentStatus = "Unpaid",
            TotalAmount = amountVnd,
            Currency = "VND",
            ReceiverName = User.FindFirst(ClaimTypes.Name)?.Value ?? "Customer",
            ReceiverPhone = "0900000000",
            StreetAddress = "Topup Online",
            District = "Topup",
            City = "TP HCM",
            CreatedAtUtc = now
        };
        db.Orders.Add(topupOrder);

        var expiresAtUtc = now.AddMinutes(15);
        var attempt = PaymentAttempt.Create(
            topupOrder.Id,
            gateway.GatewayName,
            merchantReference,
            amountVnd,
            "VND",
            expiresAtUtc,
            now,
            clientRequestId: Guid.NewGuid().ToString("N"),
            requestFingerprint: $"TOPUP_{pkg}"
        );
        db.PaymentAttempts.Add(attempt);
        await db.SaveChangesAsync(ct);

        var checkoutResult = await gateway.CreateCheckoutAsync(new CreateCheckoutRequest(
            topupOrder.Id,
            merchantReference,
            amountVnd,
            "VND",
            Description: merchantReference.Length > 25 ? merchantReference[..25] : merchantReference,
            ReturnUrl: "https://print3d.theboys.vn/pricing/topup-success",
            CancelUrl: "https://print3d.theboys.vn/pricing/topup-cancel"
        ), ct);

        if (checkoutResult.Success)
        {
            attempt.SetCheckoutAction(checkoutResult.CheckoutUrl, checkoutResult.ProviderTransactionId, checkoutResult.QrCode);
            await db.SaveChangesAsync(ct);
        }

        return Ok(new TopupPaymentResponse
        {
            Success = checkoutResult.Success,
            PackageId = pkg,
            AmountVnd = amountVnd,
            Credits = credits,
            MerchantReference = merchantReference,
            CheckoutUrl = checkoutResult.CheckoutUrl,
            QrCode = checkoutResult.QrCode
        });
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
        var isManager = User.IsInRole(Roles.Manager);
        var canApproveRefund = (User.IsInRole(Roles.Staff) || User.IsInRole(Roles.Admin)) && !isManager;

        var command = new CancelOrderV2Command(
            OrderId: id,
            UserId: UserId,
            Reason: request.Reason,
            IsManagement: IsManagement,
            DecisionNote: request.DecisionNote,
            CanApproveRefund: canApproveRefund
        );
        var result = await mediator.Send(command, ct);

        if (result.IsFailure)
        {
            if (result.Error.Code == "Forbidden")
            {
                return Forbid();
            }
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
        var isManager = User.IsInRole(Roles.Manager);
        var canApproveRefund = (User.IsInRole(Roles.Staff) || User.IsInRole(Roles.Admin)) && !isManager;

        if (isManager && request.Approved)
        {
            return Forbid();
        }

        var command = new ReviewCancellationRequestCommand(
            RequestId: requestId,
            DecidedByUserId: UserId,
            Approved: request.Approved,
            DecisionNote: request.DecisionNote,
            CanApproveRefund: canApproveRefund
        );

        var result = await mediator.Send(command, ct);
        if (result.IsFailure)
        {
            if (result.Error.Code == "Forbidden")
            {
                return Forbid();
            }
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

