using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Application.Common.Models;
using TheBoys.Application.Common.Payments;
using TheBoys.Application.Payments.DTOs;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;

namespace TheBoys.Application.Payments.Commands.ReconcileRefund;

public sealed record ReconcileRefundCommand(
    Guid RefundId,
    Guid ReconciledByUserId,
    string? Note
) : IRequest<Result<RefundDto>>;

public sealed class ReconcileRefundCommandHandler(
    IApplicationDbContext db,
    IPaymentGatewayRegistry gatewayRegistry,
    IClock clock
) : IRequestHandler<ReconcileRefundCommand, Result<RefundDto>>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task<Result<RefundDto>> Handle(ReconcileRefundCommand command, CancellationToken ct)
    {
        var refund = await db.Refunds
            .Include(r => r.PaymentAttempt)
                .ThenInclude(a => a.Refunds)
            .Include(r => r.PaymentAttempt)
                .ThenInclude(a => a.Order)
                    .ThenInclude(o => o.CancellationRequests)
            .SingleOrDefaultAsync(r => r.Id == command.RefundId, ct);

        if (refund == null)
            return Result<RefundDto>.Failure(Error.NotFound("RefundNotFound", "Không tìm thấy bản ghi hoàn tiền."));

        if (refund.Status == RefundStatus.Succeeded)
        {
            return Result<RefundDto>.Success(MapToDto(refund));
        }

        if (refund.Status != RefundStatus.Submitted && refund.Status != RefundStatus.NeedsAttention)
        {
            return Result<RefundDto>.Failure(Error.Conflict(
                "InvalidRefundStatus",
                $"Chỉ có thể đối soát hoàn tiền khi ở trạng thái Submitted hoặc NeedsAttention (hiện tại là {refund.Status})."
            ));
        }

        var attempt = refund.PaymentAttempt;
        var order = attempt.Order;
        var gateway = gatewayRegistry.ResolveGateway(attempt.Gateway);
        var now = clock.UtcNow;

        var queryResult = await gateway.QueryRefundAsync(attempt.MerchantReference, refund.OperationKey, ct);

        if (queryResult.Success && string.Equals(queryResult.Status, "Succeeded", StringComparison.OrdinalIgnoreCase))
        {
            refund.MarkSucceeded(
                queryResult.ProviderReference ?? $"RECONCILED-REF-{refund.OperationKey:N}",
                queryResult.CompletedAtUtc ?? now,
                command.ReconciledByUserId
            );

            var totalSucceeded = attempt.Refunds
                .Where(r => r.Status == RefundStatus.Succeeded || r.Id == refund.Id)
                .Sum(r => r.AmountVnd);

            var prevPaymentStatus = order.OrderPaymentStatus;
            var isFullyRefunded = totalSucceeded >= attempt.AmountVnd;
            order.OrderPaymentStatus = isFullyRefunded ? "Refunded" : "PartiallyRefunded";

            var history = OrderStatusHistory.Create(
                orderId: order.Id,
                axis: "Payment",
                fromStatus: prevPaymentStatus,
                toStatus: order.OrderPaymentStatus,
                actorUserId: command.ReconciledByUserId,
                source: "Staff",
                reason: $"Đối soát xác nhận hoàn tiền thành công: {command.Note ?? "Reconciled OK"}",
                operationKey: $"reconcile:refund:success:{refund.Id}",
                createdAtUtc: now
            );
            db.OrderStatusHistories.Add(history);

            var approvedCancellation = order.CancellationRequests
                .FirstOrDefault(c => c.RefundId == refund.Id || (c.Status == CancellationRequestStatus.Approved && c.RefundId == null));
            if (approvedCancellation != null)
            {
                approvedCancellation.RefundId = refund.Id;
                approvedCancellation.Complete(now);
            }

            var auditLog = AuditLog.Create(
                actorUserId: command.ReconciledByUserId,
                action: "ReconcileRefundSucceeded",
                aggregateType: "Refund",
                aggregateId: refund.Id,
                traceId: Guid.NewGuid().ToString("N"),
                redactedChangesJson: JsonSerializer.Serialize(new { fromStatus = "NeedsAttention", toStatus = "Succeeded", fromPayment = prevPaymentStatus, toPayment = order.OrderPaymentStatus, note = command.Note }, JsonOptions),
                reason: command.Note ?? "Đối soát xác nhận hoàn tiền thành công",
                utcNow: now
            );
            db.AuditLogs.Add(auditLog);

            var outbox = OutboxMessage.Create(
                eventType: "RefundReconciledSucceeded",
                aggregateId: order.Id,
                deduplicationKey: $"refund:reconciled:succeeded:{refund.Id}",
                payloadJson: JsonSerializer.Serialize(new
                {
                    refundId = refund.Id,
                    orderId = order.Id,
                    amountVnd = refund.AmountVnd,
                    reconciledByUserId = command.ReconciledByUserId
                }, JsonOptions),
                utcNow: now
            );
            db.OutboxMessages.Add(outbox);
        }
        else
        {
            refund.MarkFailed(queryResult.ErrorMessage ?? "Đối soát xác nhận cổng chưa thực hiện hoàn tiền.");

            // Đặt lại payment status của order nếu đang bị treo ở NeedsAttention
            if (order.OrderPaymentStatus == "NeedsAttention")
            {
                var isCancelled = order.FulfillmentStatus == "Cancelled" || order.Status == OrderStatus.Cancelled;
                var prevPaymentStatus = order.OrderPaymentStatus;
                order.OrderPaymentStatus = isCancelled ? "RefundPending" : "Paid";

                var history = OrderStatusHistory.Create(
                    orderId: order.Id,
                    axis: "Payment",
                    fromStatus: prevPaymentStatus,
                    toStatus: order.OrderPaymentStatus,
                    actorUserId: command.ReconciledByUserId,
                    source: "Staff",
                    reason: $"Đối soát ghi nhận hoàn tiền thất bại: {command.Note ?? "Refund Failed"}",
                    operationKey: $"reconcile:refund:failed:{refund.Id}",
                    createdAtUtc: now
                );
                db.OrderStatusHistories.Add(history);
            }

            var auditLog = AuditLog.Create(
                actorUserId: command.ReconciledByUserId,
                action: "ReconcileRefundFailed",
                aggregateType: "Refund",
                aggregateId: refund.Id,
                traceId: Guid.NewGuid().ToString("N"),
                redactedChangesJson: JsonSerializer.Serialize(new { fromStatus = "NeedsAttention", toStatus = "Failed", note = command.Note }, JsonOptions),
                reason: command.Note ?? "Đối soát ghi nhận hoàn tiền thất bại",
                utcNow: now
            );
            db.AuditLogs.Add(auditLog);
        }

        await db.SaveChangesAsync(ct);

        return Result<RefundDto>.Success(MapToDto(refund));
    }

    private static RefundDto MapToDto(Refund r) => new(
        r.Id,
        r.PaymentAttemptId,
        r.RequestedByUserId,
        r.ApprovedByUserId,
        r.OperationKey,
        r.ProviderReference,
        r.AmountVnd,
        r.Status.ToString(),
        r.Reason,
        r.CreatedAtUtc,
        r.CompletedAtUtc
    );
}
