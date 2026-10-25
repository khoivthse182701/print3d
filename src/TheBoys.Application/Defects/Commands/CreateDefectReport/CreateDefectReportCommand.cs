using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Application.Common.Models;
using TheBoys.Application.Defects.DTOs;
using TheBoys.Domain.Entities;

namespace TheBoys.Application.Defects.Commands.CreateDefectReport;

public sealed record DefectEvidenceInput(string StorageKey, string? Sha256Hex = null);

public sealed record CreateDefectReportResult(DefectReportDto Report, bool IsReplay);

public sealed record CreateDefectReportCommand(
    Guid OrderId,
    Guid UserId,
    Guid ClientRequestId,
    string Reason,
    List<DefectEvidenceInput>? Evidence = null,
    bool IsManagement = false
) : IRequest<Result<CreateDefectReportResult>>;

public sealed class CreateDefectReportCommandHandler(
    IApplicationDbContext db,
    IClock clock
) : IRequestHandler<CreateDefectReportCommand, Result<CreateDefectReportResult>>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task<Result<CreateDefectReportResult>> Handle(CreateDefectReportCommand command, CancellationToken ct)
    {
        var order = await db.Orders
            .SingleOrDefaultAsync(o => o.Id == command.OrderId, ct);

        if (order == null)
            return Result<CreateDefectReportResult>.Failure(Error.NotFound("OrderNotFound", "Không tìm thấy đơn hàng."));

        if (!command.IsManagement && order.UserId != command.UserId)
            return Result<CreateDefectReportResult>.Failure(Error.Unauthorized("Forbidden", "Bạn không có quyền khiếu nại đơn hàng này."));

        // Idempotency: Kiểm tra nếu (UserId, ClientRequestId) đã tồn tại
        var existingReport = await db.DefectReports
            .Include(d => d.Evidence)
            .SingleOrDefaultAsync(d => d.UserId == command.UserId && d.ClientRequestId == command.ClientRequestId, ct);

        if (existingReport != null)
        {
            return Result<CreateDefectReportResult>.Success(new CreateDefectReportResult(MapToDto(existingReport), IsReplay: true));
        }

        // Gatekeeper: Chỉ đơn ở trạng thái Delivered, Shipping hoặc DeliveryFailed mới được khiếu nại
        var validFulfillmentStatuses = new[] { "Delivered", "Shipping", "DeliveryFailed" };
        if (!validFulfillmentStatuses.Contains(order.FulfillmentStatus))
        {
            return Result<CreateDefectReportResult>.Failure(Error.Conflict(
                "OrderNotDeliveredYet",
                $"Đơn hàng ở trạng thái fulfillment '{order.FulfillmentStatus}' chưa đủ điều kiện gửi khiếu nại sản phẩm lỗi."
            ));
        }

        var now = clock.UtcNow;

        var report = DefectReport.Create(
            orderId: order.Id,
            userId: command.UserId,
            clientRequestId: command.ClientRequestId,
            reason: command.Reason,
            createdAtUtc: now
        );

        db.DefectReports.Add(report);

        if (command.Evidence != null && command.Evidence.Count > 0)
        {
            foreach (var ev in command.Evidence)
            {
                byte[] sha256Bytes;
                if (!string.IsNullOrWhiteSpace(ev.Sha256Hex) && ev.Sha256Hex.Length == 64)
                {
                    try
                    {
                        sha256Bytes = Convert.FromHexString(ev.Sha256Hex);
                    }
                    catch
                    {
                        sha256Bytes = SHA256.HashData(Encoding.UTF8.GetBytes(ev.StorageKey));
                    }
                }
                else
                {
                    sha256Bytes = SHA256.HashData(Encoding.UTF8.GetBytes(ev.StorageKey));
                }

                var evidenceEntity = DefectEvidence.Create(
                    reportId: report.Id,
                    storageKey: ev.StorageKey,
                    sha256: sha256Bytes,
                    createdAtUtc: now
                );
                report.Evidence.Add(evidenceEntity);
            }
        }

        var auditLog = AuditLog.Create(
            actorUserId: command.UserId,
            action: "CreateDefectReport",
            aggregateType: "DefectReport",
            aggregateId: report.Id,
            traceId: Guid.NewGuid().ToString("N"),
            redactedChangesJson: JsonSerializer.Serialize(new
            {
                orderId = order.Id,
                clientRequestId = command.ClientRequestId,
                reason = command.Reason,
                evidenceCount = command.Evidence?.Count ?? 0
            }, JsonOptions),
            reason: $"Khiếu nại sản phẩm lỗi cho đơn {order.OrderCode}: {command.Reason}",
            utcNow: now
        );
        db.AuditLogs.Add(auditLog);

        var outbox = OutboxMessage.Create(
            eventType: "DefectReportCreated",
            aggregateId: order.Id,
            deduplicationKey: $"defect:created:{report.Id}",
            payloadJson: JsonSerializer.Serialize(new
            {
                reportId = report.Id,
                orderId = order.Id,
                userId = command.UserId,
                reason = command.Reason
            }, JsonOptions),
            utcNow: now
        );
        db.OutboxMessages.Add(outbox);

        await db.SaveChangesAsync(ct);

        return Result<CreateDefectReportResult>.Success(new CreateDefectReportResult(MapToDto(report), IsReplay: false));
    }

    private static DefectReportDto MapToDto(DefectReport d) => new(
        d.Id,
        d.OrderId,
        d.UserId,
        d.ClientRequestId,
        d.Reason,
        d.Status.ToString(),
        d.ResolutionNote,
        d.ResolvedByUserId,
        d.RefundId,
        d.ReprintRunId,
        d.CreatedAtUtc,
        d.ResolvedAtUtc,
        d.Evidence.Select(e => new DefectEvidenceDto(
            e.Id,
            e.ReportId,
            e.StorageKey,
            Convert.ToHexString(e.Sha256),
            e.CreatedAtUtc
        )).ToList()
    );
}
