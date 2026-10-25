using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Application.Common.Models;
using TheBoys.Application.Fulfillment.DTOs;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;

namespace TheBoys.Application.Fulfillment.Commands.UpdateProductionRunProgress;

public sealed record UpdateProductionRunProgressCommand(
    Guid RunId,
    Guid ActorUserId,
    ProductionRunStatus NewStatus
) : IRequest<Result<ProductionRunDto>>;

public sealed class UpdateProductionRunProgressCommandHandler(
    IApplicationDbContext db,
    IClock clock
) : IRequestHandler<UpdateProductionRunProgressCommand, Result<ProductionRunDto>>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task<Result<ProductionRunDto>> Handle(UpdateProductionRunProgressCommand command, CancellationToken ct)
    {
        var run = await db.ProductionRuns
            .Include(r => r.Assignment)
                .ThenInclude(a => a.Order)
            .SingleOrDefaultAsync(r => r.Id == command.RunId, ct);

        if (run == null)
            return Result<ProductionRunDto>.Failure(Error.NotFound("ProductionRunNotFound", "Không tìm thấy lượt in."));

        if (run.Status == ProductionRunStatus.Passed || run.Status == ProductionRunStatus.Failed || run.Status == ProductionRunStatus.Cancelled)
            return Result<ProductionRunDto>.Failure(Error.Conflict("RunAlreadyCompleted", "Lượt in này đã kết thúc, không thể thay đổi tiến độ."));

        if (command.NewStatus != ProductionRunStatus.PostProcessing && command.NewStatus != ProductionRunStatus.QualityCheck)
            return Result<ProductionRunDto>.Failure(Error.Validation("InvalidStatus", "Chỉ hỗ trợ chuyển sang PostProcessing hoặc QualityCheck qua API tiến độ."));

        var now = clock.UtcNow;
        var order = run.Assignment.Order;
        var prevFulfillment = order.FulfillmentStatus;

        if (command.NewStatus == ProductionRunStatus.PostProcessing)
        {
            run.AdvanceToPostProcessing();
            order.FulfillmentStatus = "PostProcessing";
        }
        else if (command.NewStatus == ProductionRunStatus.QualityCheck)
        {
            run.AdvanceToQualityCheck();
            order.FulfillmentStatus = "QualityCheck";
        }

        var history = OrderStatusHistory.Create(
            orderId: order.Id,
            axis: "Fulfillment",
            fromStatus: prevFulfillment,
            toStatus: order.FulfillmentStatus,
            actorUserId: command.ActorUserId,
            source: "Partner",
            reason: $"Chuyển tiến độ lượt in #{run.RunNo} sang {command.NewStatus}",
            operationKey: $"run:progress:{run.Id}:{command.NewStatus}",
            createdAtUtc: now
        );
        db.OrderStatusHistories.Add(history);

        var auditLog = AuditLog.Create(
            actorUserId: command.ActorUserId,
            action: "UpdateProductionRunProgress",
            aggregateType: "ProductionRun",
            aggregateId: run.Id,
            traceId: Guid.NewGuid().ToString("N"),
            redactedChangesJson: JsonSerializer.Serialize(new
            {
                runId = run.Id,
                newStatus = command.NewStatus.ToString()
            }, JsonOptions),
            reason: $"Cập nhật tiến độ lượt in #{run.RunNo} của đơn {order.OrderCode}",
            utcNow: now
        );
        db.AuditLogs.Add(auditLog);

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

