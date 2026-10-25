using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Application.Common.Models;
using TheBoys.Application.Fulfillment.DTOs;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;

namespace TheBoys.Application.Fulfillment.Commands.AssignOrderToPartner;

public sealed record AssignOrderToPartnerCommand(
    Guid OrderId,
    Guid PartnerLabId,
    Guid AssignedByUserId,
    decimal? PartnerCostSnapshotVnd = null,
    DateTime? DueAtUtc = null,
    string? DecisionNote = null
) : IRequest<Result<PartnerAssignmentDto>>;

public sealed class AssignOrderToPartnerCommandHandler(
    IApplicationDbContext db,
    IClock clock
) : IRequestHandler<AssignOrderToPartnerCommand, Result<PartnerAssignmentDto>>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task<Result<PartnerAssignmentDto>> Handle(AssignOrderToPartnerCommand command, CancellationToken ct)
    {
        await using var tx = await db.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);

        var lockAcquired = await db.AcquireApplicationLockAsync($"Ops_Order_{command.OrderId}", TimeSpan.FromSeconds(15), ct);
        if (!lockAcquired)
        {
            await tx.RollbackAsync(ct);
            return Result<PartnerAssignmentDto>.Failure(Error.Conflict(
                "OrderLockConflict",
                "Đơn hàng đang được điều phối bởi một tác vụ khác, vui lòng thử lại sau."
            ));
        }

        var order = await db.Orders
            .Include(o => o.PartnerAssignments)
            .SingleOrDefaultAsync(o => o.Id == command.OrderId, ct);

        if (order == null)
        {
            await tx.RollbackAsync(ct);
            return Result<PartnerAssignmentDto>.Failure(Error.NotFound("OrderNotFound", "Không tìm thấy đơn hàng."));
        }

        // Gatekeeper: Chỉ đơn đã Paid và đang AwaitingAssignment mới được phân công
        if (order.OrderPaymentStatus != "Paid")
        {
            await tx.RollbackAsync(ct);
            return Result<PartnerAssignmentDto>.Failure(Error.Conflict(
                "OrderNotEligibleForAssignment",
                "Chỉ có thể giao xưởng cho đơn hàng đã thanh toán thành công."
            ));
        }

        if (order.FulfillmentStatus != "AwaitingAssignment")
        {
            await tx.RollbackAsync(ct);
            return Result<PartnerAssignmentDto>.Failure(Error.Conflict(
                "OrderNotEligibleForAssignment",
                $"Đơn hàng ở trạng thái fulfillment '{order.FulfillmentStatus}' không đủ điều kiện phân công xưởng."
            ));
        }

        var partnerLab = await db.PartnerLabs
            .SingleOrDefaultAsync(l => l.Id == command.PartnerLabId, ct);

        if (partnerLab == null || !partnerLab.IsActive)
        {
            await tx.RollbackAsync(ct);
            return Result<PartnerAssignmentDto>.Failure(Error.Conflict(
                "PartnerLabInactive",
                "Xưởng in không tồn tại hoặc đã ngừng hoạt động."
            ));
        }

        var activeAssignmentExists = await db.PartnerAssignments
            .AnyAsync(a => a.OrderId == command.OrderId && a.IsActive, ct);

        if (activeAssignmentExists)
        {
            await tx.RollbackAsync(ct);
            return Result<PartnerAssignmentDto>.Failure(Error.Conflict(
                "ActiveAssignmentExists",
                "Đơn hàng đã có một phân công xưởng đang hoạt động."
            ));
        }

        var now = clock.UtcNow;
        var cost = command.PartnerCostSnapshotVnd ?? (order.PartnerCostSnapshot > 0 ? order.PartnerCostSnapshot : 120000m);
        var dueAt = command.DueAtUtc ?? now.AddDays(3);

        var assignment = PartnerAssignment.Create(
            orderId: order.Id,
            partnerLabId: partnerLab.Id,
            assignedByUserId: command.AssignedByUserId,
            partnerCostSnapshotVnd: cost,
            dueAtUtc: dueAt,
            decisionNote: command.DecisionNote,
            createdAtUtc: now
        );

        db.PartnerAssignments.Add(assignment);

        var prevFulfillment = order.FulfillmentStatus;
        order.FulfillmentStatus = "Assigned";
        order.PartnerLabId = partnerLab.Id;
        order.SentToPartnerAtUtc = now;

        var history = OrderStatusHistory.Create(
            orderId: order.Id,
            axis: "Fulfillment",
            fromStatus: prevFulfillment,
            toStatus: "Assigned",
            actorUserId: command.AssignedByUserId,
            source: "Staff",
            reason: $"Phân công cho xưởng in: {partnerLab.LabName}",
            operationKey: $"assignment:create:{assignment.Id}",
            createdAtUtc: now
        );
        db.OrderStatusHistories.Add(history);

        var auditLog = AuditLog.Create(
            actorUserId: command.AssignedByUserId,
            action: "AssignOrderToPartner",
            aggregateType: "PartnerAssignment",
            aggregateId: assignment.Id,
            traceId: Guid.NewGuid().ToString("N"),
            redactedChangesJson: JsonSerializer.Serialize(new
            {
                orderId = order.Id,
                partnerLabId = partnerLab.Id,
                labName = partnerLab.LabName,
                costVnd = cost,
                dueAtUtc = dueAt
            }, JsonOptions),
            reason: $"Phân công đơn {order.OrderCode} cho xưởng {partnerLab.LabName}",
            utcNow: now
        );
        db.AuditLogs.Add(auditLog);

        var outbox = OutboxMessage.Create(
            eventType: "OrderAssignedToPartner",
            aggregateId: order.Id,
            deduplicationKey: $"order:assigned:{assignment.Id}",
            payloadJson: JsonSerializer.Serialize(new
            {
                assignmentId = assignment.Id,
                orderId = order.Id,
                orderCode = order.OrderCode,
                partnerLabId = partnerLab.Id,
                labName = partnerLab.LabName,
                dueAtUtc = dueAt
            }, JsonOptions),
            utcNow: now
        );
        db.OutboxMessages.Add(outbox);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Result<PartnerAssignmentDto>.Success(new PartnerAssignmentDto(
            assignment.Id,
            assignment.OrderId,
            assignment.PartnerLabId,
            partnerLab.LabName,
            assignment.AssignedByUserId,
            assignment.Status.ToString(),
            assignment.IsActive,
            assignment.PartnerCostSnapshotVnd,
            assignment.DueAtUtc,
            assignment.DecisionNote,
            assignment.CreatedAtUtc,
            new List<ProductionRunDto>()
        ));
    }
}

