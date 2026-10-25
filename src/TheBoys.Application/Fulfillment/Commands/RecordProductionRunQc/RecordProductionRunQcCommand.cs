using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Application.Common.Models;
using TheBoys.Application.Fulfillment.DTOs;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;

namespace TheBoys.Application.Fulfillment.Commands.RecordProductionRunQc;

public sealed record RecordProductionRunQcCommand(
    Guid RunId,
    Guid ActorUserId,
    bool Passed,
    string? QcEvidenceStorageKey,
    string? QcNote,
    bool TriggerReprint = false,
    decimal? ActualCostVnd = null
) : IRequest<Result<ProductionRunDto>>;

public sealed class RecordProductionRunQcCommandHandler(
    IApplicationDbContext db,
    IClock clock
) : IRequestHandler<RecordProductionRunQcCommand, Result<ProductionRunDto>>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task<Result<ProductionRunDto>> Handle(RecordProductionRunQcCommand command, CancellationToken ct)
    {
        var run = await db.ProductionRuns
            .Include(r => r.Assignment)
                .ThenInclude(a => a.Order)
            .Include(r => r.Assignment)
                .ThenInclude(a => a.ProductionRuns)
            .SingleOrDefaultAsync(r => r.Id == command.RunId, ct);

        if (run == null)
            return Result<ProductionRunDto>.Failure(Error.NotFound("ProductionRunNotFound", "Không tìm thấy lượt in."));

        if (run.Status == ProductionRunStatus.Passed || run.Status == ProductionRunStatus.Failed || run.Status == ProductionRunStatus.Cancelled)
            return Result<ProductionRunDto>.Failure(Error.Conflict("RunAlreadyCompleted", "Lượt in này đã hoàn thành kiểm định QC trước đó."));

        var now = clock.UtcNow;
        var assignment = run.Assignment;
        var order = assignment.Order;

        if (command.Passed)
        {
            // Bất biến: QC Pass bắt buộc phải có ảnh/bằng chứng QC và ghi chú
            if (string.IsNullOrWhiteSpace(command.QcEvidenceStorageKey) || string.IsNullOrWhiteSpace(command.QcNote))
            {
                return Result<ProductionRunDto>.Failure(Error.Conflict(
                    "QcEvidenceRequired",
                    "Đánh dấu QC Đạt bắt buộc phải có đường dẫn bằng chứng lưu trữ (QcEvidenceStorageKey) và ghi chú đánh giá (QcNote)."
                ));
            }

            run.MarkPassed(command.QcEvidenceStorageKey, command.QcNote, now, command.ActualCostVnd);
            assignment.Complete("QC đạt chuẩn chất lượng sản xuất");

            var prevFulfillment = order.FulfillmentStatus;
            order.FulfillmentStatus = "Packaged";
            order.PackagedAtUtc = now;

            var history = OrderStatusHistory.Create(
                orderId: order.Id,
                axis: "Fulfillment",
                fromStatus: prevFulfillment,
                toStatus: "Packaged",
                actorUserId: command.ActorUserId,
                source: "Staff",
                reason: $"QC Đạt lượt in #{run.RunNo}. Đóng gói hoàn tất: {command.QcNote}",
                operationKey: $"qc:passed:{run.Id}",
                createdAtUtc: now
            );
            db.OrderStatusHistories.Add(history);

            var auditLog = AuditLog.Create(
                actorUserId: command.ActorUserId,
                action: "ProductionRunQcPassed",
                aggregateType: "ProductionRun",
                aggregateId: run.Id,
                traceId: Guid.NewGuid().ToString("N"),
                redactedChangesJson: JsonSerializer.Serialize(new
                {
                    runId = run.Id,
                    qcEvidenceStorageKey = command.QcEvidenceStorageKey,
                    qcNote = command.QcNote
                }, JsonOptions),
                reason: $"QC Đạt lượt in #{run.RunNo} của đơn {order.OrderCode}",
                utcNow: now
            );
            db.AuditLogs.Add(auditLog);

            var outbox = OutboxMessage.Create(
                eventType: "OrderPassedQcAndPackaged",
                aggregateId: order.Id,
                deduplicationKey: $"order:packaged:{order.Id}:{run.Id}",
                payloadJson: JsonSerializer.Serialize(new
                {
                    orderId = order.Id,
                    runId = run.Id,
                    runNo = run.RunNo,
                    packagedAtUtc = now
                }, JsonOptions),
                utcNow: now
            );
            db.OutboxMessages.Add(outbox);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(command.QcNote))
            {
                return Result<ProductionRunDto>.Failure(Error.Conflict(
                    "QcNoteRequired",
                    "Đánh dấu QC Không Đạt yêu cầu phải có ghi chú nguyên nhân lỗi (QcNote)."
                ));
            }

            run.MarkFailed(command.QcNote, command.QcEvidenceStorageKey, now, command.ActualCostVnd);

            var prevFulfillment = order.FulfillmentStatus;

            var auditLog = AuditLog.Create(
                actorUserId: command.ActorUserId,
                action: "ProductionRunQcFailed",
                aggregateType: "ProductionRun",
                aggregateId: run.Id,
                traceId: Guid.NewGuid().ToString("N"),
                redactedChangesJson: JsonSerializer.Serialize(new
                {
                    runId = run.Id,
                    qcNote = command.QcNote,
                    triggerReprint = command.TriggerReprint
                }, JsonOptions),
                reason: $"QC Thất bại lượt in #{run.RunNo}: {command.QcNote}",
                utcNow: now
            );
            db.AuditLogs.Add(auditLog);

            if (command.TriggerReprint)
            {
                // Tự động tạo lượt in tiếp theo cho assignment
                var nextRunNo = (assignment.ProductionRuns.Any() ? assignment.ProductionRuns.Max(r => r.RunNo) : run.RunNo) + 1;
                var reprintRun = ProductionRun.Start(
                    assignmentId: assignment.Id,
                    runNo: nextRunNo,
                    quantity: run.Quantity,
                    startedAtUtc: now
                );
                db.ProductionRuns.Add(reprintRun);

                order.FulfillmentStatus = "Printing";

                var reprintHistory = OrderStatusHistory.Create(
                    orderId: order.Id,
                    axis: "Fulfillment",
                    fromStatus: prevFulfillment,
                    toStatus: "Printing",
                    actorUserId: command.ActorUserId,
                    source: "Staff",
                    reason: $"QC không đạt lượt in #{run.RunNo} ({command.QcNote}). Kích hoạt tái in lượt #{nextRunNo}.",
                    operationKey: $"qc:failed:reprint:{run.Id}:{nextRunNo}",
                    createdAtUtc: now
                );
                db.OrderStatusHistories.Add(reprintHistory);
            }
            else
            {
                var failHistory = OrderStatusHistory.Create(
                    orderId: order.Id,
                    axis: "Fulfillment",
                    fromStatus: prevFulfillment,
                    toStatus: order.FulfillmentStatus,
                    actorUserId: command.ActorUserId,
                    source: "Staff",
                    reason: $"QC không đạt lượt in #{run.RunNo}: {command.QcNote}",
                    operationKey: $"qc:failed:{run.Id}",
                    createdAtUtc: now
                );
                db.OrderStatusHistories.Add(failHistory);
            }
        }

        await db.SaveChangesAsync(ct);

        return Result<ProductionRunDto>.Success(new ProductionRunDto(
            run.Id,
            run.AssignmentId,
            run.RunNo,
            run.Status.ToString(),
            run.Quantity,
            run.ActualCostVnd,
            run.QcEvidenceStorageKey,
            run.QcNote,
            run.StartedAtUtc,
            run.CompletedAtUtc
        ));
    }
}

