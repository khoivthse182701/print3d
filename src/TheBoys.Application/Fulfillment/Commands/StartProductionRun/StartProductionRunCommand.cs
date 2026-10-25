using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Application.Common.Models;
using TheBoys.Application.Fulfillment.DTOs;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;

namespace TheBoys.Application.Fulfillment.Commands.StartProductionRun;

public sealed record StartProductionRunCommand(
    Guid AssignmentId,
    Guid ActorUserId,
    int? Quantity = null,
    decimal? ActualCostVnd = null
) : IRequest<Result<ProductionRunDto>>;

public sealed class StartProductionRunCommandHandler(
    IApplicationDbContext db,
    IClock clock
) : IRequestHandler<StartProductionRunCommand, Result<ProductionRunDto>>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task<Result<ProductionRunDto>> Handle(StartProductionRunCommand command, CancellationToken ct)
    {
        var assignment = await db.PartnerAssignments
            .Include(a => a.Order)
            .Include(a => a.ProductionRuns)
            .SingleOrDefaultAsync(a => a.Id == command.AssignmentId, ct);

        if (assignment == null)
            return Result<ProductionRunDto>.Failure(Error.NotFound("AssignmentNotFound", "Không tìm thấy phân công xưởng."));

        // Bất biến: Chỉ được tạo/bắt đầu ProductionRun khi PartnerAssignment đã Accepted & IsActive
        if (!assignment.IsActive || assignment.Status != PartnerAssignmentStatus.Accepted)
        {
            return Result<ProductionRunDto>.Failure(Error.Conflict(
                "PartnerNotAcceptedYet",
                "Xưởng in chưa chấp nhận đơn hàng hoặc phân công không còn hiệu lực."
            ));
        }

        var order = assignment.Order;

        // Bất biến: Đơn hàng phải ở trạng thái Paid
        if (order.OrderPaymentStatus != "Paid")
        {
            return Result<ProductionRunDto>.Failure(Error.Conflict(
                "OrderNotPaid",
                "Chỉ có thể bắt đầu lượt in cho đơn hàng đã thanh toán thành công."
            ));
        }

        // Bất biến: Không được tạo chồng chéo nhiều ProductionRun đang chạy
        var hasActiveRun = assignment.ProductionRuns.Any(r =>
            r.Status == ProductionRunStatus.Printing ||
            r.Status == ProductionRunStatus.PostProcessing ||
            r.Status == ProductionRunStatus.QualityCheck);

        if (hasActiveRun)
        {
            return Result<ProductionRunDto>.Failure(Error.Conflict(
                "ActiveProductionRunExists",
                "Phân công này đang có một lượt in chưa hoàn tất, không thể khởi tạo lượt in mới."
            ));
        }

        var now = clock.UtcNow;
        var existingMaxRunNo = assignment.ProductionRuns.Any()
            ? assignment.ProductionRuns.Max(r => r.RunNo)
            : 0;
        var runNo = existingMaxRunNo + 1;
        var quantity = command.Quantity ?? (order.Quantity > 0 ? order.Quantity : 1);

        var run = ProductionRun.Start(
            assignmentId: assignment.Id,
            runNo: runNo,
            quantity: quantity,
            startedAtUtc: now,
            actualCostVnd: command.ActualCostVnd
        );

        db.ProductionRuns.Add(run);

        var prevFulfillment = order.FulfillmentStatus;
        order.FulfillmentStatus = "Printing";
        order.PrintingStartedAtUtc ??= now;

        var history = OrderStatusHistory.Create(
            orderId: order.Id,
            axis: "Fulfillment",
            fromStatus: prevFulfillment,
            toStatus: "Printing",
            actorUserId: command.ActorUserId,
            source: "Partner",
            reason: $"Bắt đầu lượt in #{runNo} (Số lượng: {quantity})",
            operationKey: $"run:start:{run.Id}",
            createdAtUtc: now
        );
        db.OrderStatusHistories.Add(history);

        var auditLog = AuditLog.Create(
            actorUserId: command.ActorUserId,
            action: "StartProductionRun",
            aggregateType: "ProductionRun",
            aggregateId: run.Id,
            traceId: Guid.NewGuid().ToString("N"),
            redactedChangesJson: JsonSerializer.Serialize(new
            {
                assignmentId = assignment.Id,
                orderId = order.Id,
                runNo = runNo,
                quantity = quantity,
                actualCostVnd = command.ActualCostVnd
            }, JsonOptions),
            reason: $"Bắt đầu lượt in #{runNo} cho đơn {order.OrderCode}",
            utcNow: now
        );
        db.AuditLogs.Add(auditLog);

        var outbox = OutboxMessage.Create(
            eventType: "ProductionRunStarted",
            aggregateId: order.Id,
            deduplicationKey: $"run:started:{run.Id}",
            payloadJson: JsonSerializer.Serialize(new
            {
                runId = run.Id,
                assignmentId = assignment.Id,
                orderId = order.Id,
                runNo = runNo,
                quantity = quantity
            }, JsonOptions),
            utcNow: now
        );
        db.OutboxMessages.Add(outbox);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Result<ProductionRunDto>.Failure(Error.Conflict(
                "ProductionRunConflict",
                "Đã có lượt in được tạo với số thứ tự tương ứng, vui lòng làm mới dữ liệu."
            ));
        }

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

