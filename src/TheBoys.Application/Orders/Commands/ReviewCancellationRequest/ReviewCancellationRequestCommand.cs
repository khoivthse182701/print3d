using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Application.Common.Models;
using TheBoys.Application.Orders.DTOs;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;

namespace TheBoys.Application.Orders.Commands.ReviewCancellationRequest;

public sealed record ReviewCancellationRequestCommand(
    Guid RequestId,
    Guid DecidedByUserId,
    bool Approved,
    string? DecisionNote
) : IRequest<Result<OrderCancellationRequestDto>>;

public sealed class ReviewCancellationRequestCommandHandler(
    IApplicationDbContext db,
    IClock clock
) : IRequestHandler<ReviewCancellationRequestCommand, Result<OrderCancellationRequestDto>>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task<Result<OrderCancellationRequestDto>> Handle(ReviewCancellationRequestCommand command, CancellationToken ct)
    {
        var cancelReq = await db.OrderCancellationRequests
            .Include(c => c.Order)
            .SingleOrDefaultAsync(c => c.Id == command.RequestId, ct);

        if (cancelReq == null)
            return Result<OrderCancellationRequestDto>.Failure(Error.NotFound("CancellationRequestNotFound", "Không tìm thấy yêu cầu hủy đơn hàng."));

        if (cancelReq.Status != CancellationRequestStatus.Pending)
            return Result<OrderCancellationRequestDto>.Failure(Error.Conflict("CancellationRequestNotPending", "Yêu cầu hủy đơn này đã được xử lý trước đó."));

        var now = clock.UtcNow;
        var order = cancelReq.Order;

        if (command.Approved)
        {
            cancelReq.Approve(command.DecidedByUserId, command.DecisionNote, null, now);

            order.Status = OrderStatus.Cancelled;
            var prevFulfillment = order.FulfillmentStatus;
            order.FulfillmentStatus = "Cancelled";
            var prevPayment = order.OrderPaymentStatus;
            order.OrderPaymentStatus = "RefundPending";
            order.CancelledAtUtc = now;
            order.StatusNote = $"Duyệt yêu cầu hủy đơn bởi quản trị: {command.DecisionNote ?? cancelReq.Reason}";

            var historyFulfillment = OrderStatusHistory.Create(
                orderId: order.Id,
                axis: "Fulfillment",
                fromStatus: prevFulfillment,
                toStatus: "Cancelled",
                actorUserId: command.DecidedByUserId,
                source: "Staff",
                reason: $"Phê duyệt yêu cầu hủy đơn #{cancelReq.Id}",
                operationKey: $"review:cancellation:fulfillment:{cancelReq.Id}",
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
                    actorUserId: command.DecidedByUserId,
                    source: "Staff",
                    reason: "Chờ hoàn tiền sau khi duyệt yêu cầu hủy",
                    operationKey: $"review:cancellation:payment:{cancelReq.Id}",
                    createdAtUtc: now
                );
                db.OrderStatusHistories.Add(historyPayment);
            }

            var auditLog = AuditLog.Create(
                actorUserId: command.DecidedByUserId,
                action: "ApproveOrderCancellation",
                aggregateType: "OrderCancellationRequest",
                aggregateId: cancelReq.Id,
                traceId: Guid.NewGuid().ToString("N"),
                redactedChangesJson: JsonSerializer.Serialize(new { fromStatus = "Pending", toStatus = "Approved", note = command.DecisionNote }, JsonOptions),
                reason: command.DecisionNote ?? "Phê duyệt hủy đơn",
                utcNow: now
            );
            db.AuditLogs.Add(auditLog);

            var outbox = OutboxMessage.Create(
                eventType: "OrderCancellationApproved",
                aggregateId: order.Id,
                deduplicationKey: $"cancellation:approved:{cancelReq.Id}",
                payloadJson: JsonSerializer.Serialize(new
                {
                    requestId = cancelReq.Id,
                    orderId = order.Id,
                    decidedByUserId = command.DecidedByUserId,
                    note = command.DecisionNote
                }, JsonOptions),
                utcNow: now
            );
            db.OutboxMessages.Add(outbox);
        }
        else
        {
            cancelReq.Reject(command.DecidedByUserId, command.DecisionNote, now);

            var auditLog = AuditLog.Create(
                actorUserId: command.DecidedByUserId,
                action: "RejectOrderCancellation",
                aggregateType: "OrderCancellationRequest",
                aggregateId: cancelReq.Id,
                traceId: Guid.NewGuid().ToString("N"),
                redactedChangesJson: JsonSerializer.Serialize(new { fromStatus = "Pending", toStatus = "Rejected", note = command.DecisionNote }, JsonOptions),
                reason: command.DecisionNote ?? "Từ chối hủy đơn",
                utcNow: now
            );
            db.AuditLogs.Add(auditLog);

            var outbox = OutboxMessage.Create(
                eventType: "OrderCancellationRejected",
                aggregateId: order.Id,
                deduplicationKey: $"cancellation:rejected:{cancelReq.Id}",
                payloadJson: JsonSerializer.Serialize(new
                {
                    requestId = cancelReq.Id,
                    orderId = order.Id,
                    decidedByUserId = command.DecidedByUserId,
                    note = command.DecisionNote
                }, JsonOptions),
                utcNow: now
            );
            db.OutboxMessages.Add(outbox);
        }

        await db.SaveChangesAsync(ct);

        return Result<OrderCancellationRequestDto>.Success(new OrderCancellationRequestDto(
            cancelReq.Id,
            cancelReq.OrderId,
            cancelReq.RequestedByUserId,
            cancelReq.Reason,
            cancelReq.Status.ToString(),
            cancelReq.DecidedByUserId,
            cancelReq.DecisionNote,
            cancelReq.RefundId,
            cancelReq.CreatedAtUtc,
            cancelReq.DecidedAtUtc
        ));
    }
}
