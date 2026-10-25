using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Application.Common.Models;
using TheBoys.Application.Common.Payments;
using TheBoys.Application.Payments.Commands.CreatePaymentAttempt;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;

namespace TheBoys.Application.Payments.Commands.ReconcilePaymentAttempt;

public sealed class ReconcilePaymentAttemptCommandHandler(
    IApplicationDbContext db,
    IPaymentGatewayRegistry gatewayRegistry,
    IClock clock) : IRequestHandler<ReconcilePaymentAttemptCommand, Result<ReconcilePaymentAttemptResult>>
{
    public async Task<Result<ReconcilePaymentAttemptResult>> Handle(ReconcilePaymentAttemptCommand request, CancellationToken ct)
    {
        var attempt = await db.PaymentAttempts
            .Include(pa => pa.Order)
            .SingleOrDefaultAsync(pa => pa.Id == request.PaymentAttemptId, ct);

        if (attempt == null)
        {
            return Result<ReconcilePaymentAttemptResult>.Failure(Error.NotFound("AttemptNotFound", "Không tìm thấy PaymentAttempt."));
        }

        var order = attempt.Order;
        var now = clock.UtcNow;

        IPaymentGateway gateway;
        try
        {
            gateway = gatewayRegistry.ResolveGateway(attempt.Gateway);
        }
        catch (Exception)
        {
            return Result<ReconcilePaymentAttemptResult>.Failure(Error.NotFound("GatewayNotFound", $"Không tìm thấy gateway '{attempt.Gateway}'."));
        }

        if (!gateway.IsConfigured)
        {
            return Result<ReconcilePaymentAttemptResult>.Success(new(
                CreatePaymentAttemptCommandHandler.MapToDto(attempt),
                "Gateway chưa được cấu hình. Giữ nguyên trạng thái để staff xử lý thủ công."
            ));
        }

        var queryResult = await gateway.QueryPaymentAsync(attempt.MerchantReference, ct);
        if (!queryResult.Success)
        {
            return Result<ReconcilePaymentAttemptResult>.Success(new(
                CreatePaymentAttemptCommandHandler.MapToDto(attempt),
                $"Gateway không hỗ trợ đối soát tự động ({queryResult.RawStatus}). Giữ nguyên trạng thái để staff xử lý thủ công."
            ));
        }

        if (string.Equals(queryResult.Status, "Succeeded", StringComparison.OrdinalIgnoreCase))
        {
            if (order.OrderPaymentStatus != "Paid" && order.FulfillmentStatus == "PendingPayment")
            {
                attempt.MarkSucceeded(queryResult.ProviderTransactionId, queryResult.TransactionTimeUtc ?? now);
                order.OrderPaymentStatus = "Paid";
                order.FulfillmentStatus = "AwaitingAssignment";
                order.PaidAtUtc = now;
                order.Status = OrderStatus.Paid;

                var histPayment = OrderStatusHistory.Create(
                    order.Id,
                    axis: "Payment",
                    fromStatus: "Unpaid",
                    toStatus: "Paid",
                    actorUserId: request.StaffUserId,
                    source: "Reconciliation",
                    reason: $"Đối soát thành công qua {attempt.Gateway} (TxId: {queryResult.ProviderTransactionId ?? "N/A"}).",
                    operationKey: $"reconcile-pay-{attempt.Id:N}",
                    createdAtUtc: now
                );
                db.OrderStatusHistories.Add(histPayment);

                var histFulfillment = OrderStatusHistory.Create(
                    order.Id,
                    axis: "Fulfillment",
                    fromStatus: "PendingPayment",
                    toStatus: "AwaitingAssignment",
                    actorUserId: request.StaffUserId,
                    source: "Reconciliation",
                    reason: "Thanh toán đối soát thành công, chuyển sang hàng đợi phân công xưởng in.",
                    operationKey: $"reconcile-fulfill-{attempt.Id:N}",
                    createdAtUtc: now
                );
                db.OrderStatusHistories.Add(histFulfillment);

                var outbox = OutboxMessage.Create(
                    "PaymentConfirmed",
                    order.Id,
                    $"payment-reconciled:{attempt.Id:N}",
                    JsonSerializer.Serialize(new { orderId = order.Id, attemptId = attempt.Id, gateway = attempt.Gateway }),
                    now
                );
                db.OutboxMessages.Add(outbox);

                var audit = AuditLog.Create(
                    request.StaffUserId,
                    "PaymentReconciled",
                    "PaymentAttempt",
                    attempt.Id,
                    Guid.NewGuid().ToString("N"),
                    JsonSerializer.Serialize(new { orderId = order.Id, attemptId = attempt.Id, txId = queryResult.ProviderTransactionId }),
                    "Staff triggered reconciliation succeeded",
                    now
                );
                db.AuditLogs.Add(audit);

                await db.SaveChangesAsync(ct);
                return Result<ReconcilePaymentAttemptResult>.Success(new(CreatePaymentAttemptCommandHandler.MapToDto(attempt), "Đối soát thành công, đơn hàng đã được cập nhật Paid."));
            }
        }
        else if (string.Equals(queryResult.Status, "Failed", StringComparison.OrdinalIgnoreCase))
        {
            attempt.MarkFailed("Đối soát xác nhận giao dịch thất bại tại gateway.");
            await db.SaveChangesAsync(ct);
            return Result<ReconcilePaymentAttemptResult>.Success(new(CreatePaymentAttemptCommandHandler.MapToDto(attempt), "Đối soát xác nhận giao dịch thất bại."));
        }

        return Result<ReconcilePaymentAttemptResult>.Success(new(CreatePaymentAttemptCommandHandler.MapToDto(attempt), "Đối soát hoàn tất."));
    }
}

