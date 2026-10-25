using MediatR;
using Microsoft.EntityFrameworkCore;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Application.Common.Models;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;

namespace TheBoys.Application.Defects.Queries;

public sealed record DefectReportDto(
    Guid Id,
    Guid OrderId,
    string OrderCode,
    string CustomerName,
    string Reason,
    List<string> EvidencePhotos,
    string Status,
    string? ResolutionNote,
    DateTime CreatedAtUtc,
    DateTime? ResolvedAtUtc,
    Guid? UserId = null,
    Guid? RefundId = null,
    Guid? ReprintRunId = null
)
{
    public List<string> Evidence => EvidencePhotos;
}

public sealed record GetDefectReportsQuery(
    string? Status = null,
    Guid? OrderId = null
) : IRequest<Result<List<DefectReportDto>>>;

public sealed class GetDefectReportsQueryHandler(
    IApplicationDbContext db
) : IRequestHandler<GetDefectReportsQuery, Result<List<DefectReportDto>>>
{
    public async Task<Result<List<DefectReportDto>>> Handle(GetDefectReportsQuery query, CancellationToken ct)
    {
        var queryable = db.DefectReports.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Status) && Enum.TryParse<DefectReportStatus>(query.Status, true, out var statusEnum))
        {
            queryable = queryable.Where(d => d.Status == statusEnum);
        }

        if (query.OrderId.HasValue && query.OrderId.Value != Guid.Empty)
        {
            queryable = queryable.Where(d => d.OrderId == query.OrderId.Value);
        }

        var reports = await queryable
            .Include(d => d.Order)
            .Include(d => d.User)
            .Include(d => d.Evidence)
            .OrderByDescending(d => d.CreatedAtUtc)
            .ToListAsync(ct);

        var dtos = reports.Select(d =>
        {
            var customerName = !string.IsNullOrWhiteSpace(d.User?.FullName)
                ? d.User.FullName
                : (!string.IsNullOrWhiteSpace(d.Order?.ReceiverName) ? d.Order.ReceiverName : "Khách hàng");

            var evidencePhotos = d.Evidence != null && d.Evidence.Count > 0
                ? d.Evidence.OrderBy(e => e.CreatedAtUtc).Select(e => e.StorageKey).ToList()
                : (!string.IsNullOrWhiteSpace(d.Order?.EvidencePhotoUrl) ? new List<string> { d.Order.EvidencePhotoUrl! } : new List<string>());

            return new DefectReportDto(
                Id: d.Id,
                OrderId: d.OrderId,
                OrderCode: d.Order?.OrderCode ?? string.Empty,
                CustomerName: customerName,
                Reason: d.Reason,
                EvidencePhotos: evidencePhotos,
                Status: d.Status.ToString(),
                ResolutionNote: d.ResolutionNote,
                CreatedAtUtc: d.CreatedAtUtc,
                ResolvedAtUtc: d.ResolvedAtUtc,
                UserId: d.UserId,
                RefundId: d.RefundId,
                ReprintRunId: d.ReprintRunId
            );
        }).ToList();

        return Result<List<DefectReportDto>>.Success(dtos);
    }
}
