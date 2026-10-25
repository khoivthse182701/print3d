using MediatR;
using Microsoft.EntityFrameworkCore;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Application.Staff.Models;

namespace TheBoys.Application.Staff.Queries.GetStaffGenerationJobs;

public sealed class GetStaffGenerationJobsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetStaffGenerationJobsQuery, StaffPagedResult<StaffGenerationJobItem>>
{
    public async Task<StaffPagedResult<StaffGenerationJobItem>> Handle(GetStaffGenerationJobsQuery request, CancellationToken ct)
    {
        var query = db.GenerationTasks.AsNoTracking();

        if (request.Status.HasValue)
        {
            query = query.Where(x => x.Status == request.Status.Value);
        }

        if (request.ReviewStatus.HasValue)
        {
            query = query.Where(x => x.ReviewStatus == request.ReviewStatus.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Provider))
        {
            var p = request.Provider.Trim().ToLower();
            query = query.Where(x => x.Provider.ToLower() == p);
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
            var cutoff = request.CapturedUtcNow.AddMinutes(-request.MinAgeMinutes.Value);
            query = query.Where(x => x.CreatedAtUtc <= cutoff);
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
                x.ModelName,
                x.Provider,
                x.Status,
                x.Stage,
                x.Progress,
                x.ReviewStatus,
                x.CreatedAtUtc,
                x.CompletedAtUtc
            })
            .ToListAsync(ct);

        var items = records.Select(x => new StaffGenerationJobItem(
            x.Id,
            x.ModelName,
            x.Provider,
            x.Status,
            x.Stage,
            x.Progress,
            x.ReviewStatus,
            x.CreatedAtUtc,
            x.CompletedAtUtc,
            Math.Max(0, (long)Math.Floor((request.CapturedUtcNow - x.CreatedAtUtc).TotalMinutes))
        )).ToList();

        return new StaffPagedResult<StaffGenerationJobItem>(items, request.Page, request.PageSize, totalCount);
    }
}
