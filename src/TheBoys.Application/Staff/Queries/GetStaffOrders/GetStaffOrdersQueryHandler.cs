using MediatR;
using Microsoft.EntityFrameworkCore;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Application.Staff.Models;

namespace TheBoys.Application.Staff.Queries.GetStaffOrders;

public sealed class GetStaffOrdersQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetStaffOrdersQuery, StaffPagedResult<StaffOrderItem>>
{
    public async Task<StaffPagedResult<StaffOrderItem>> Handle(GetStaffOrdersQuery request, CancellationToken ct)
    {
        var query = db.Orders.AsNoTracking();

        if (request.Status.HasValue)
        {
            query = query.Where(x => x.Status == request.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.OrderCode))
        {
            var code = request.OrderCode.Trim();
            query = query.Where(x => x.OrderCode == code);
        }

        if (request.CreatedFromUtc.HasValue)
        {
            query = query.Where(x => x.CreatedAtUtc >= request.CreatedFromUtc.Value);
        }

        if (request.CreatedToUtc.HasValue)
        {
            query = query.Where(x => x.CreatedAtUtc < request.CreatedToUtc.Value);
        }

        if (request.MinAgeMinutes.HasValue)
        {
            var maxMinutes = (request.CapturedUtcNow - DateTime.MinValue).TotalMinutes;
            if (request.MinAgeMinutes.Value >= maxMinutes)
            {
                query = query.Where(x => false);
            }
            else
            {
                var cutoff = request.CapturedUtcNow.AddMinutes(-request.MinAgeMinutes.Value);
                query = query.Where(x => x.CreatedAtUtc <= cutoff);
            }
        }

        var totalCount = await query.CountAsync(ct);

        if (request.Sort.Equals("newest", StringComparison.OrdinalIgnoreCase))
        {
            query = query.OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id);
        }
        else
        {
            query = query.OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id);
        }

        var skip = (request.Page - 1) * request.PageSize;
        var records = await query
            .Skip(skip)
            .Take(request.PageSize)
            .Select(x => new
            {
                x.Id,
                x.OrderCode,
                x.GenerationTaskId,
                x.Status,
                x.Quantity,
                x.CreatedAtUtc,
                x.PaidAtUtc,
                x.SentToPartnerAtUtc,
                x.PrintingStartedAtUtc,
                x.PackagedAtUtc,
                x.ShippedAtUtc,
                x.DeliveredAtUtc,
                x.CancelledAtUtc
            })
            .ToListAsync(ct);

        var items = records.Select(x => new StaffOrderItem(
            x.Id,
            x.OrderCode,
            x.GenerationTaskId ?? Guid.Empty,
            x.Status,
            x.Quantity,
            DateTime.SpecifyKind(x.CreatedAtUtc, DateTimeKind.Utc),
            Math.Max(0, (long)Math.Floor((request.CapturedUtcNow - x.CreatedAtUtc).TotalMinutes)),
            AsUtc(x.PaidAtUtc),
            AsUtc(x.SentToPartnerAtUtc),
            AsUtc(x.PrintingStartedAtUtc),
            AsUtc(x.PackagedAtUtc),
            AsUtc(x.ShippedAtUtc),
            AsUtc(x.DeliveredAtUtc),
            AsUtc(x.CancelledAtUtc)
        )).ToList();

        return new StaffPagedResult<StaffOrderItem>(items, request.Page, request.PageSize, totalCount);
    }

    private static DateTime? AsUtc(DateTime? dt) => dt.HasValue ? DateTime.SpecifyKind(dt.Value, DateTimeKind.Utc) : null;
}
