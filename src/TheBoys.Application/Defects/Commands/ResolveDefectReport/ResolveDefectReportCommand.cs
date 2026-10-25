using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Application.Common.Models;
using TheBoys.Application.Defects.DTOs;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;

namespace TheBoys.Application.Defects.Commands.ResolveDefectReport;

public enum DefectResolutionType
{
    ReprintApproved = 1,
    RefundApproved = 2,
    Rejected = 3
}

public sealed record ResolveDefectReportCommand(
    Guid ReportId,
    Guid ActorUserId,
    DefectResolutionType ResolutionType,
    string? ResolutionNote = null
) : IRequest<Result<DefectReportDto>>;

public sealed class ResolveDefectReportCommandHandler(
    IApplicationDbContext db,
    IClock clock
) : IRequestHandler<ResolveDefectReportCommand, Result<DefectReportDto>>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task<Result<DefectReportDto>> Handle(ResolveDefectReportCommand command, CancellationToken ct)
    {
        var report = await db.DefectReports
            .Include(d => d.Order)
                .ThenInclude(o => o.PartnerAssignments)
                    .ThenInclude(a => a.ProductionRuns)
            .Include(d => d.Evidence)
            .SingleOrDefaultAsync(d => d.Id == command.ReportId, ct);

        if (report == null)
            return Result<DefectReportDto>.Failure(Error.NotFound("DefectReportNotFound", "Không tìm thấy yêu cầu khiếu nại."));

        if (report.Status != DefectReportStatus.Open && report.Status != DefectReportStatus.Investigating && report.Status != DefectReportStatus.AwaitingEvidence)
            return Result<DefectReportDto>.Failure(Error.Conflict("ReportAlreadyDecided", "Khiếu nại này đã có quyết định xử lý trước đó."));

        var now = clock.UtcNow;
        var order = report.Order;

        if (command.ResolutionType == DefectResolutionType.ReprintApproved)
        {
            // Tìm assignment có sẵn hoặc kích hoạt lại để gắn lượt in mới
            var assignment = order.PartnerAssignments.FirstOrDefault(a => a.IsActive && a.Status == PartnerAssignmentStatus.Accepted)
                ?? order.PartnerAssignments.OrderByDescending(a => a.CreatedAtUtc).FirstOrDefault();

            if (assignment == null)
            {
                return Result<DefectReportDto>.Failure(Error.Conflict("NoAssignmentForReprint", "Không tìm thấy phân công xưởng nào của đơn hàng để kích hoạt in lại."));
            }

            // Nếu assignment cũ đã Completed, ta tái kích hoạt lại Accepted/IsActive để tuân thủ CK_PartnerAssignments_ActiveState
            if (!assignment.IsActive)
            {
                assignment.Accept("Tái kích hoạt phân công để in lại theo khiếu nại bảo hành");
            }

            var maxRun = assignment.ProductionRuns.Any() ? assignment.ProductionRuns.Max(r => r.RunNo) : 0;
            var nextRunNo = maxRun + 1;

            var reprintRun = ProductionRun.Start(
                assignmentId: assignment.Id,
                runNo: nextRunNo,
                quantity: order.Quantity,
                startedAtUtc: now
            );
            db.ProductionRuns.Add(reprintRun);

            report.ApproveReprint(command.ActorUserId, reprintRun.Id, command.ResolutionNote, now);

            var prevFulfillment = order.FulfillmentStatus;
            order.FulfillmentStatus = "Printing";
            order.ReprintApproved = true;

            var history = OrderStatusHistory.Create(
                orderId: order.Id,
                axis: "Fulfillment",
                fromStatus: prevFulfillment,
                toStatus: "Printing",
                actorUserId: command.ActorUserId,
                source: "Staff",
                reason: $"Duyệt in lại theo khiếu nại #{report.Id}: {command.ResolutionNote ?? "Chấp thuận bảo hành"}. Bắt đầu lượt in #{nextRunNo}.",
                operationKey: $"defect:reprint:{report.Id}:{reprintRun.Id}",
                createdAtUtc: now
            );
            db.OrderStatusHistories.Add(history);

            var auditLog = AuditLog.Create(
                actorUserId: command.ActorUserId,
                action: "ApproveDefectReprint",
                aggregateType: "DefectReport",
                aggregateId: report.Id,
                traceId: Guid.NewGuid().ToString("N"),
                redactedChangesJson: JsonSerializer.Serialize(new
                {
                    reportId = report.Id,
                    reprintRunId = reprintRun.Id,
                    nextRunNo = nextRunNo,
                    resolutionNote = command.ResolutionNote
                }, JsonOptions),
                reason: $"Duyệt in lại cho khiếu nại đơn {order.OrderCode}",
                utcNow: now
            );
            db.AuditLogs.Add(auditLog);

            var outbox = OutboxMessage.Create(
                eventType: "DefectReprintApproved",
                aggregateId: order.Id,
                deduplicationKey: $"defect:reprint:{report.Id}",
                payloadJson: JsonSerializer.Serialize(new
                {
                    reportId = report.Id,
                    orderId = order.Id,
                    reprintRunId = reprintRun.Id,
                    runNo = nextRunNo
                }, JsonOptions),
                utcNow: now
            );
            db.OutboxMessages.Add(outbox);
        }
        else if (command.ResolutionType == DefectResolutionType.RefundApproved)
        {
            report.ApproveRefund(command.ActorUserId, null, command.ResolutionNote, now);

            var prevPayment = order.OrderPaymentStatus;
            order.OrderPaymentStatus = "RefundPending";

            var history = OrderStatusHistory.Create(
                orderId: order.Id,
                axis: "Payment",
                fromStatus: prevPayment,
                toStatus: "RefundPending",
                actorUserId: command.ActorUserId,
                source: "Staff",
                reason: $"Duyệt hoàn tiền theo khiếu nại: {command.ResolutionNote ?? "Chấp thuận hoàn tiền bảo hành"}",
                operationKey: $"defect:refund:{report.Id}",
                createdAtUtc: now
            );
            db.OrderStatusHistories.Add(history);

            var auditLog = AuditLog.Create(
                actorUserId: command.ActorUserId,
                action: "ApproveDefectRefund",
                aggregateType: "DefectReport",
                aggregateId: report.Id,
                traceId: Guid.NewGuid().ToString("N"),
                redactedChangesJson: JsonSerializer.Serialize(new
                {
                    reportId = report.Id,
                    resolutionNote = command.ResolutionNote
                }, JsonOptions),
                reason: $"Duyệt hoàn tiền cho khiếu nại đơn {order.OrderCode}",
                utcNow: now
            );
            db.AuditLogs.Add(auditLog);

            var outbox = OutboxMessage.Create(
                eventType: "DefectRefundApproved",
                aggregateId: order.Id,
                deduplicationKey: $"defect:refund:{report.Id}",
                payloadJson: JsonSerializer.Serialize(new
                {
                    reportId = report.Id,
                    orderId = order.Id,
                    resolutionNote = command.ResolutionNote
                }, JsonOptions),
                utcNow: now
            );
            db.OutboxMessages.Add(outbox);
        }
        else
        {
            report.Reject(command.ActorUserId, command.ResolutionNote, now);

            var auditLog = AuditLog.Create(
                actorUserId: command.ActorUserId,
                action: "RejectDefectReport",
                aggregateType: "DefectReport",
                aggregateId: report.Id,
                traceId: Guid.NewGuid().ToString("N"),
                redactedChangesJson: JsonSerializer.Serialize(new
                {
                    reportId = report.Id,
                    resolutionNote = command.ResolutionNote
                }, JsonOptions),
                reason: $"Từ chối khiếu nại đơn {order.OrderCode}: {command.ResolutionNote ?? "Không đủ cơ sở lỗi sản xuất"}",
                utcNow: now
            );
            db.AuditLogs.Add(auditLog);
        }

        await db.SaveChangesAsync(ct);

        return Result<DefectReportDto>.Success(new DefectReportDto(
            report.Id,
            report.OrderId,
            report.UserId,
            report.ClientRequestId,
            report.Reason,
            report.Status.ToString(),
            report.ResolutionNote,
            report.ResolvedByUserId,
            report.RefundId,
            report.ReprintRunId,
            report.CreatedAtUtc,
            report.ResolvedAtUtc,
            report.Evidence.Select(e => new DefectEvidenceDto(
                e.Id,
                e.ReportId,
                e.StorageKey,
                Convert.ToHexString(e.Sha256),
                e.CreatedAtUtc
            )).ToList()
        ));
    }
}

