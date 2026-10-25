using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Application.Common.Models;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;

namespace TheBoys.Application.Orders.Commands.CancelOrderV2;

public sealed record CancelOrderV2Command(
    Guid OrderId,
    Guid UserId,
    string Reason,
    bool IsManagement,
    string? DecisionNote = null
) : IRequest<Result<CancelOrderV2Result>>;

public sealed record CancelOrderV2Result(
    Guid OrderId,
    string OrderCode,
    string FulfillmentStatus,
    string PaymentStatus,
    string CancellationStatus,
    Guid? CancellationRequestId,
    bool RefundRequired
);

public sealed class CancelOrderV2CommandHandler(
    IApplicationDbContext db,
    IClock clock
) : IRequestHandler<CancelOrderV2Command, Result<CancelOrderV2Result>>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task<Result<CancelOrderV2Result>> Handle(CancelOrderV2Command request, CancellationToken ct)
    {
        var order = await db.Orders
            .Include(o => o.PaymentAttempts)
            .Include(o => o.CancellationRequests)
            .SingleOrDefaultAsync(o => o.Id == request.OrderId, ct);

        if (order == null)
            return Result<CancelOrderV2Result>.Failure(Error.NotFound("OrderNotFound", "Không tìm thấy đơn hàng."));

        if (!request.IsManagement && order.UserId != request.UserId)
            return Result<CancelOrderV2Result>.Failure(Error.Unauthorized("Forbidden", "Bạn không có quyền thao tác trên đơn hàng này."));

        if (string.IsNullOrWhiteSpace(request.Reason))
            return Result<CancelOrderV2Result>.Failure(Error.Validation("ReasonRequired", "Lý do hủy đơn không được để trống."));

        if (order.FulfillmentStatus == "Cancelled" || order.Status == OrderStatus.Cancelled)
            return Result<CancelOrderV2Result>.Failure(Error.Conflict("OrderAlreadyCancelled", "Đơn hàng đã ở trạng thái đã hủy."));

        var now = clock.UtcNow;

        // Trường hợp 1: Đơn chưa thanh toán (Unpaid & PendingPayment hoặc Draft)
        var isUnpaid = string.Equals(order.OrderPaymentStatus, "Unpaid", StringComparison.OrdinalIgnoreCase);
        if (isUnpaid)
        {
            var prevFulfillment = order.FulfillmentStatus;
            order.Status = OrderStatus.Cancelled;
            order.FulfillmentStatus = "Cancelled";
            order.CancelledAtUtc = now;
            order.StatusNote = $"Hủy đơn chưa thanh toán: {request.Reason}";

            foreach (var attempt in order.PaymentAttempts.Where(a => a.Status == PaymentAttemptStatus.Pending))
            {
                attempt.MarkExpired();
            }

            var history = OrderStatusHistory.Create(
                orderId: order.Id,
                axis: "Fulfillment",
                fromStatus: prevFulfillment,
                toStatus: "Cancelled",
                actorUserId: request.UserId,
                source: request.IsManagement ? "Staff" : "Customer",
                reason: request.Reason,
                operationKey: $"cancel:unpaid:{order.Id}:{Guid.NewGuid():N}",
                createdAtUtc: now
            );
            db.OrderStatusHistories.Add(history);

            var outbox = OutboxMessage.Create(
                eventType: "OrderCancelled",
                aggregateId: order.Id,
                deduplicationKey: $"order:cancelled:{order.Id}:{now.Ticks}",
                payloadJson: JsonSerializer.Serialize(new
                {
                    orderId = order.Id,
                    orderCode = order.OrderCode,
                    reason = request.Reason,
                    cancelledByUserId = request.UserId
                }, JsonOptions),
                utcNow: now
            );
            db.OutboxMessages.Add(outbox);

            await db.SaveChangesAsync(ct);

            return Result<CancelOrderV2Result>.Success(new CancelOrderV2Result(
                order.Id,
                order.OrderCode,
                order.FulfillmentStatus,
                order.OrderPaymentStatus,
                "CancelledDirectly",
                null,
                false
            ));
        }

        // Trường hợp 2: Đơn đã thanh toán (Paid hoặc RefundPending)
        var inProduction = order.FulfillmentStatus is "Printing" or "PostProcessing" or "QualityCheck" or "Packaged" or "Shipping" or "Shipped" or "Delivered"
                           || order.PrintingStartedAtUtc != null;

        if (inProduction)
        {
            if (!request.IsManagement)
            {
                return Result<CancelOrderV2Result>.Failure(Error.Conflict(
                    "CannotCancelInProduction",
                    "Đơn hàng đã bắt đầu sản xuất hoặc vận chuyển, không thể tự hủy. Vui lòng liên hệ bộ phận hỗ trợ."
                ));
            }
        }

        if (!request.IsManagement)
        {
            // Khách hàng yêu cầu hủy đơn đã thanh toán: Tạo OrderCancellationRequest ở trạng thái Pending
            var existingPending = order.CancellationRequests.FirstOrDefault(c => c.Status == CancellationRequestStatus.Pending);
            if (existingPending != null)
            {
                return Result<CancelOrderV2Result>.Failure(Error.Conflict(
                    "CancellationRequestPending",
                    "Yêu cầu hủy đơn hàng của bạn đang được xử lý."
                ));
            }

            var cancelReq = OrderCancellationRequest.Create(order.Id, request.UserId, request.Reason, now);
            db.OrderCancellationRequests.Add(cancelReq);
            order.CancellationRequestedAtUtc = now;

            var outboxReq = OutboxMessage.Create(
                eventType: "OrderCancellationRequested",
                aggregateId: order.Id,
                deduplicationKey: $"order:cancellation:requested:{cancelReq.Id}",
                payloadJson: JsonSerializer.Serialize(new
                {
                    requestId = cancelReq.Id,
                    orderId = order.Id,
                    orderCode = order.OrderCode,
                    requestedByUserId = request.UserId,
                    reason = request.Reason
                }, JsonOptions),
                utcNow: now
            );
            db.OutboxMessages.Add(outboxReq);

            await db.SaveChangesAsync(ct);

            return Result<CancelOrderV2Result>.Success(new CancelOrderV2Result(
                order.Id,
                order.OrderCode,
                order.FulfillmentStatus,
                order.OrderPaymentStatus,
                "PendingReview",
                cancelReq.Id,
                true
            ));
        }
        else
        {
            // Staff/Admin duyệt hủy trực tiếp đơn đã thanh toán: Tạo & Duyệt OrderCancellationRequest
            var cancelReq = OrderCancellationRequest.Create(order.Id, request.UserId, request.Reason, now);
            cancelReq.Approve(request.UserId, request.DecisionNote ?? "Staff approved immediate cancellation", null, now);
            db.OrderCancellationRequests.Add(cancelReq);

            order.Status = OrderStatus.Cancelled;
            var prevFulfillment = order.FulfillmentStatus;
            order.FulfillmentStatus = "Cancelled";
            var prevPayment = order.OrderPaymentStatus;
            order.OrderPaymentStatus = "RefundPending";
            order.CancelledAtUtc = now;
            order.StatusNote = $"Đã duyệt hủy đơn bởi Quản trị: {request.Reason}";

            var historyFulfillment = OrderStatusHistory.Create(
                orderId: order.Id,
                axis: "Fulfillment",
                fromStatus: prevFulfillment,
                toStatus: "Cancelled",
                actorUserId: request.UserId,
                source: "Staff",
                reason: request.Reason,
                operationKey: $"cancel:paid:fulfillment:{order.Id}:{Guid.NewGuid():N}",
                createdAtUtc: now
            );
            db.OrderStatusHistories.Add(historyFulfillment);

            if (!string.Equals(prevPayment, "RefundPending", StringComparison.OrdinalIgnoreCase))
            {
                var historyPayment = OrderStatusHistory.Create(
                    orderId: order.Id,
                    axis: "Payment",
                    fromStatus: prevPayment,
                    toStatus: "RefundPending",
                    actorUserId: request.UserId,
                    source: "Staff",
                    reason: "Chờ hoàn tiền sau khi duyệt hủy đơn",
                    operationKey: $"cancel:paid:payment:{order.Id}:{Guid.NewGuid():N}",
                    createdAtUtc: now
                );
                db.OrderStatusHistories.Add(historyPayment);
            }

            var auditLog = AuditLog.Create(
                actorUserId: request.UserId,
                action: "CancelPaidOrder",
                aggregateType: "Order",
                aggregateId: order.Id,
                traceId: Guid.NewGuid().ToString("N"),
                redactedChangesJson: JsonSerializer.Serialize(new { fromFulfillment = prevFulfillment, toFulfillment = "Cancelled", fromPayment = prevPayment, toPayment = "RefundPending" }, JsonOptions),
                reason: request.Reason,
                utcNow: now
            );
            db.AuditLogs.Add(auditLog);

            var outboxApproved = OutboxMessage.Create(
                eventType: "OrderCancelledAwaitingRefund",
                aggregateId: order.Id,
                deduplicationKey: $"order:cancelled:refund_pending:{order.Id}:{now.Ticks}",
                payloadJson: JsonSerializer.Serialize(new
                {
                    requestId = cancelReq.Id,
                    orderId = order.Id,
                    orderCode = order.OrderCode,
                    cancelledByUserId = request.UserId,
                    reason = request.Reason
                }, JsonOptions),
                utcNow: now
            );
            db.OutboxMessages.Add(outboxApproved);

            await db.SaveChangesAsync(ct);

            return Result<CancelOrderV2Result>.Success(new CancelOrderV2Result(
                order.Id,
                order.OrderCode,
                order.FulfillmentStatus,
                order.OrderPaymentStatus,
                "ApprovedAwaitingRefund",
                cancelReq.Id,
                true
            ));
        }
    }
}
