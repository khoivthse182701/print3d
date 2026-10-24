using MediatR;
using Microsoft.EntityFrameworkCore;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Application.Common.Models;
using TheBoys.Application.Defects.DTOs;
using TheBoys.Application.Fulfillment.DTOs;

namespace TheBoys.Application.Fulfillment.Queries.GetOrderFulfillmentTimeline;

public sealed record GetOrderFulfillmentTimelineQuery(
    Guid OrderId,
    Guid UserId,
    bool IsManagement
) : IRequest<Result<OrderFulfillmentTimelineDto>>;

public sealed class GetOrderFulfillmentTimelineQueryHandler(
    IApplicationDbContext db
) : IRequestHandler<GetOrderFulfillmentTimelineQuery, Result<OrderFulfillmentTimelineDto>>
{
    public async Task<Result<OrderFulfillmentTimelineDto>> Handle(GetOrderFulfillmentTimelineQuery query, CancellationToken ct)
    {
        var order = await db.Orders
            .Include(o => o.PartnerAssignments)
                .ThenInclude(a => a.PartnerLab)
            .Include(o => o.PartnerAssignments)
                .ThenInclude(a => a.ProductionRuns)
            .Include(o => o.Shipments)
                .ThenInclude(s => s.Events)
            .Include(o => o.DefectReports)
                .ThenInclude(d => d.Evidence)
            .SingleOrDefaultAsync(o => o.Id == query.OrderId, ct);

        if (order == null)
            return Result<OrderFulfillmentTimelineDto>.Failure(Error.NotFound("OrderNotFound", "Không tìm thấy đơn hàng."));

        if (!query.IsManagement && order.UserId != query.UserId)
            return Result<OrderFulfillmentTimelineDto>.Failure(Error.Unauthorized("Forbidden", "Bạn không có quyền xem thông tin đơn hàng này."));

        var assignmentDtos = order.PartnerAssignments
            .OrderBy(a => a.CreatedAtUtc)
            .Select(a => new PartnerAssignmentDto(
                a.Id,
                a.OrderId,
                a.PartnerLabId,
                a.PartnerLab?.LabName,
                a.AssignedByUserId,
                a.Status.ToString(),
                a.IsActive,
                a.PartnerCostSnapshotVnd,
                a.DueAtUtc,
                a.DecisionNote,
                a.CreatedAtUtc,
                a.ProductionRuns.OrderBy(r => r.RunNo).Select(r => new ProductionRunDto(
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
                )).ToList()
            )).ToList();

        var shipmentDtos = order.Shipments
            .OrderBy(s => s.CreatedAtUtc)
            .Select(s => new ShipmentDto(
                s.Id,
                s.OrderId,
                s.Carrier,
                s.TrackingNumber,
                s.Status.ToString(),
                s.ShippingActualVnd,
                s.DeliveredAtUtc,
                s.DeliveryEvidenceStorageKey,
                s.CreatedAtUtc,
                s.Events.OrderBy(e => e.OccurredAtUtc).Select(e => new ShipmentEventDto(
                    e.Id,
                    e.ShipmentId,
                    e.EventKey,
                    e.Status,
                    e.Note,
                    e.Source,
                    e.ActorUserId,
                    e.OccurredAtUtc,
                    e.ReceivedAtUtc
                )).ToList()
            )).ToList();

        var defectDtos = order.DefectReports
            .OrderBy(d => d.CreatedAtUtc)
            .Select(d => new DefectReportDto(
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
            )).ToList();

        return Result<OrderFulfillmentTimelineDto>.Success(new OrderFulfillmentTimelineDto(
            order.Id,
            order.OrderCode,
            order.FulfillmentStatus,
            order.OrderPaymentStatus,
            order.PartnerLabId,
            order.TrackingNumber,
            assignmentDtos,
            shipmentDtos,
            defectDtos
        ));
    }
}

