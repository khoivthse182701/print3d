using MediatR;
using Microsoft.EntityFrameworkCore;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Domain.Entities;

namespace TheBoys.Application.Admin.Queries;

public sealed record GetUsersQuery(
    int Page = 1,
    int PageSize = 50
) : IRequest<IReadOnlyList<User>>;

public sealed class GetUsersQueryHandler(IApplicationDbContext db) : IRequestHandler<GetUsersQuery, IReadOnlyList<User>>
{
    public async Task<IReadOnlyList<User>> Handle(GetUsersQuery request, CancellationToken ct)
    {
        return await db.Users.AsNoTracking()
            .OrderBy(x => x.CreatedAtUtc)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);
    }
}

public sealed record GetActivePricingPackagesQuery(bool IncludeInactive = false) : IRequest<IReadOnlyList<PricingPackage>>;

public sealed class GetActivePricingPackagesQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetActivePricingPackagesQuery, IReadOnlyList<PricingPackage>>
{
    public async Task<IReadOnlyList<PricingPackage>> Handle(GetActivePricingPackagesQuery request, CancellationToken ct)
    {
        var query = db.PricingPackages.AsNoTracking();
        if (!request.IncludeInactive)
        {
            query = query.Where(x => x.IsActive);
        }
        return await query.ToListAsync(ct);
    }
}
