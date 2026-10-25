using MediatR;
using Microsoft.EntityFrameworkCore;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Application.Common.Models;
using TheBoys.Application.Common.Payments;
using TheBoys.Application.Payments.DTOs;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;

namespace TheBoys.Application.Payments.Commands.CreatePaymentAttempt;

public sealed class CreatePaymentAttemptCommandHandler(
    IApplicationDbContext db,
    IPaymentGatewayRegistry gatewayRegistry,
    IClock clock) : IRequestHandler<CreatePaymentAttemptCommand, Result<CreatePaymentAttemptResult>>
{
    public async Task<Result<CreatePaymentAttemptResult>> Handle(CreatePaymentAttemptCommand request, CancellationToken ct)
    {
        // 1. Kiểm tra đơn hàng
        var order = await db.Orders
            .Include(o => o.PaymentAttempts)
            .SingleOrDefaultAsync(o => o.Id == request.OrderId, ct);

        if (order == null)
        {
            return Result<CreatePaymentAttemptResult>.Failure(Error.NotFound("OrderNotFound", "Không tìm thấy đơn hàng."));
        }

        if (order.UserId != request.UserId && !request.IsManagement)
        {
            return Result<CreatePaymentAttemptResult>.Failure(Error.Unauthorized("NotOwner", "Không thể tạo lượt thanh toán cho đơn hàng của tài khoản khác."));
        }

        if (order.FulfillmentStatus != "PendingPayment" || order.OrderPaymentStatus != "Unpaid")
        {
            return Result<CreatePaymentAttemptResult>.Failure(Error.Conflict(
                "InvalidOrderState",
                $"Chỉ đơn hàng đang ở trạng thái 'PendingPayment' và 'Unpaid' mới có thể tạo lượt thanh toán. Trạng thái hiện tại: {order.FulfillmentStatus}/{order.OrderPaymentStatus}."
            ));
        }

        // 2. Kiểm tra Idempotency trên ClientRequestId
        var existingAttempt = order.PaymentAttempts
            .FirstOrDefault(pa => string.Equals(pa.ClientRequestId, request.ClientRequestId, StringComparison.OrdinalIgnoreCase));

        if (existingAttempt != null)
        {
            if (string.Equals(existingAttempt.RequestFingerprint, request.RequestFingerprint, StringComparison.Ordinal))
            {
                return Result<CreatePaymentAttemptResult>.Success(new(MapToDto(existingAttempt), IsReplay: true));
            }

            return Result<CreatePaymentAttemptResult>.Failure(Error.Conflict(
                "IdempotencyMismatch",
                "Idempotency-Key đã dùng cho thao tác thanh toán khác."
            ));
        }

        // 3. Resolve gateway cấu hình từ server
        IPaymentGateway gateway;
        try
        {
            gateway = gatewayRegistry.ResolveActiveGateway();
        }
        catch (Exception ex)
        {
            return Result<CreatePaymentAttemptResult>.Failure(Error.ServiceUnavailable(
                "GatewayNotConfigured",
                $"Không thể khởi tạo cổng thanh toán: {ex.Message}"
            ));
        }

        if (!gateway.IsConfigured)
        {
            return Result<CreatePaymentAttemptResult>.Failure(Error.ServiceUnavailable(
                "GatewayNotConfigured",
                $"Payment gateway '{gateway.GatewayName}' chưa được cấu hình trên hệ thống."
            ));
        }

        var now = clock.UtcNow;
        var expiresAtUtc = now.AddMinutes(15);
        var merchantReference = $"TB-{now:yyyyMMdd}-{Guid.NewGuid():N}";

        // 4. Bước 1: Persist PaymentAttempt (Pending) và commit trước khi gọi gateway ngoài
        var attempt = PaymentAttempt.Create(
            order.Id,
            gateway.GatewayName,
            merchantReference,
            order.TotalAmount,
            order.Currency,
            expiresAtUtc,
            now,
            request.ClientRequestId,
            request.RequestFingerprint
        );

        db.PaymentAttempts.Add(attempt);
        await db.SaveChangesAsync(ct);

        // 5. Bước 2: Gọi gateway ngoài ngoài phạm vi transaction
        var description = string.IsNullOrWhiteSpace(request.RequestFingerprint)
            ? $"Thanh toán đơn hàng {order.OrderCode}"
            : $"Thanh toán đơn hàng {order.OrderCode} [{request.RequestFingerprint}]";

        CreateCheckoutResult checkoutResult;
        try
        {
            checkoutResult = await gateway.CreateCheckoutAsync(new CreateCheckoutRequest(
                order.Id,
                merchantReference,
                order.TotalAmount,
                order.Currency,
                description,
                ReturnUrl: $"https://print3d.theboys.vn/orders/{order.Id}/checkout-return",
                CancelUrl: $"https://print3d.theboys.vn/orders/{order.Id}/checkout-cancel"
            ), ct);
        }
        catch (Exception ex) when (ex is TimeoutException or TaskCanceledException)
        {
            attempt.MarkNeedsAttention($"Gateway checkout timeout: {ex.Message}");
            await db.SaveChangesAsync(ct);
            return Result<CreatePaymentAttemptResult>.Success(new(MapToDto(attempt), IsReplay: false));
        }
        catch (Exception ex)
        {
            attempt.MarkNeedsAttention($"Gateway checkout error: {ex.Message}");
            await db.SaveChangesAsync(ct);
            return Result<CreatePaymentAttemptResult>.Success(new(MapToDto(attempt), IsReplay: false));
        }

        if (checkoutResult.Success)
        {
            attempt.SetCheckoutAction(checkoutResult.CheckoutUrl, checkoutResult.ProviderTransactionId, checkoutResult.QrCode);
        }
        else
        {
            attempt.MarkFailed(checkoutResult.ErrorMessage ?? checkoutResult.ErrorCode ?? "Cổng thanh toán từ chối yêu cầu.");
        }

        await db.SaveChangesAsync(ct);

        return Result<CreatePaymentAttemptResult>.Success(new(MapToDto(attempt), IsReplay: false));
    }

    public static PaymentAttemptDto MapToDto(PaymentAttempt pa) =>
        new(
            pa.Id,
            pa.OrderId,
            pa.Gateway,
            pa.MerchantReference,
            pa.ProviderTransactionId,
            pa.Status.ToString(),
            pa.AmountVnd,
            pa.Currency,
            pa.ExpiresAtUtc,
            pa.CreatedAtUtc,
            pa.CapturedAtUtc,
            pa.CheckoutUrl,
            pa.FailureReason,
            pa.QrCode
        );
}
