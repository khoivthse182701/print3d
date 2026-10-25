using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Application.Common.Models;
using TheBoys.Application.Fulfillment.DTOs;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;

namespace TheBoys.Application.Fulfillment.Commands.RespondPartnerAssignment;

public sealed record RespondPartnerAssignmentCommand(
    Guid AssignmentId,
    Guid ActorUserId,
    bool Accepted,
    string? DecisionNote = null,
    bool IsManagement = true
) : IRequest<Result<PartnerAssignmentDto>>;

public sealed class RespondPartnerAssignmentCommandHandler(
    IApplicationDbContext db,
    IClock clock
) : IRequestHandler<RespondPartnerAssignmentCommand, Result<PartnerAssignmentDto>>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task<Result<PartnerAssignmentDto>> Handle(RespondPartnerAssignmentCommand command, CancellationToken ct)
    {
        if (!command.IsManagement)
        {
            return Result<PartnerAssignmentDto>.Failure(Error.Unauthorized("Forbidden", "Chỉ nhân viên hoặc quản trị viên mới có quyền phản hồi phân công xưởng."));
        }

        var assignment = await db.PartnerAssignments
            .Include(a => a.Order)
            .Include(a => a.PartnerLab)
            .Include(a => a.ProductionRuns)
            .SingleOrDefaultAsync(a => a.Id == command.AssignmentId, ct);

        if (assignment == null)
            return Result<PartnerAssignmentDto>.Failure(Error.NotFound("AssignmentNotFound", "Không tìm thấy phân công xưởng."));

        if (!assignment.IsActive || assignment.Status != PartnerAssignmentStatus.Offered)
            return Result<PartnerAssignmentDto>.Failure(Error.Conflict(
                "AssignmentNotOffered",
                "Phân công không ở trạng thái chờ phản hồi."
            ));

        var now = clock.UtcNow;
        var order = assignment.Order;

        if (command.Accepted)
        {
            assignment.Accept(command.DecisionNote);

            var auditLog = AuditLog.Create(
                actorUserId: command.ActorUserId,
                action: "PartnerAcceptedAssignment",
                aggregateType: "PartnerAssignment",
                aggregateId: assignment.Id,
                traceId: Guid.NewGuid().ToString("N"),
                redactedChangesJson: JsonSerializer.Serialize(new
                {
                    status = "Accepted",
                    decisionNote = command.DecisionNote
                }, JsonOptions),
                reason: $"Xưởng {assignment.PartnerLab.LabName} chấp nhận đơn {order.OrderCode}",
                utcNow: now
            );
            db.AuditLogs.Add(auditLog);

            var outbox = OutboxMessage.Create(
                eventType: "PartnerAcceptedOrder",
                aggregateId: order.Id,
                deduplicationKey: $"partner:accepted:{assignment.Id}",
                payloadJson: JsonSerializer.Serialize(new
                {
                    assignmentId = assignment.Id,
                    orderId = order.Id,
                    partnerLabId = assignment.PartnerLabId,
                    labName = assignment.PartnerLab.LabName
                }, JsonOptions),
                utcNow: now
            );
            db.OutboxMessages.Add(outbox);
        }
        else
        {
            assignment.Reject(command.DecisionNote);

            var prevFulfillment = order.FulfillmentStatus;
            order.FulfillmentStatus = "AwaitingAssignment";
            order.PartnerLabId = null;

            var history = OrderStatusHistory.Create(
                orderId: order.Id,
                axis: "Fulfillment",
                fromStatus: prevFulfillment,
                toStatus: "AwaitingAssignment",
                actorUserId: command.ActorUserId,
                source: "Partner",
                reason: $"Xưởng {assignment.PartnerLab.LabName} từ chối đơn: {command.DecisionNote ?? "Không rõ lý do"}",
                operationKey: $"assignment:reject:{assignment.Id}",
                createdAtUtc: now
            );
            db.OrderStatusHistories.Add(history);

            var auditLog = AuditLog.Create(
                actorUserId: command.ActorUserId,
                action: "PartnerRejectedAssignment",
                aggregateType: "PartnerAssignment",
                aggregateId: assignment.Id,
                traceId: Guid.NewGuid().ToString("N"),
                redactedChangesJson: JsonSerializer.Serialize(new
                {
                    status = "Rejected",
                    decisionNote = command.DecisionNote
                }, JsonOptions),
                reason: $"Xưởng {assignment.PartnerLab.LabName} từ chối đơn {order.OrderCode}",
                utcNow: now
            );
            db.AuditLogs.Add(auditLog);

            var outbox = OutboxMessage.Create(
                eventType: "PartnerRejectedOrder",
                aggregateId: order.Id,
                deduplicationKey: $"partner:rejected:{assignment.Id}",
                payloadJson: JsonSerializer.Serialize(new
                {
                    assignmentId = assignment.Id,
                    orderId = order.Id,
                    partnerLabId = assignment.PartnerLabId,
                    labName = assignment.PartnerLab.LabName,
                    decisionNote = command.DecisionNote
                }, JsonOptions),
                utcNow: now
            );
            db.OutboxMessages.Add(outbox);
        }

        await db.SaveChangesAsync(ct);

        var runDtos = assignment.ProductionRuns.Select(r => new ProductionRunDto(
            r.Id,
            r.AssignmentId,
            r.RunNo,
            r.Status.ToString(),
            r.Quantity,
            r.ActualCostVnd,
            r.QcEvidenceStorageKey,
            r.QcNote,
            r.StartedAtUtc,
            r.CompletedAtUtc
        )).ToList();

        return Result<PartnerAssignmentDto>.Success(new PartnerAssignmentDto(
            assignment.Id,
            assignment.OrderId,
            assignment.PartnerLabId,
            assignment.PartnerLab.LabName,
            assignment.AssignedByUserId,
            assignment.Status.ToString(),
            assignment.IsActive,
            assignment.PartnerCostSnapshotVnd,
            assignment.DueAtUtc,
            assignment.DecisionNote,
            assignment.CreatedAtUtc,
            runDtos
        ));
    }
}

