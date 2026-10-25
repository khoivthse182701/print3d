using MediatR;
using Microsoft.EntityFrameworkCore;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Application.Common.Models;
using TheBoys.Application.Fulfillment.DTOs;

namespace TheBoys.Application.Fulfillment.Queries.GetActivePartnerLabs;

public sealed record GetActivePartnerLabsQuery : IRequest<Result<List<PartnerLabSummaryDto>>>;

public sealed class GetActivePartnerLabsQueryHandler(
    IApplicationDbContext db
) : IRequestHandler<GetActivePartnerLabsQuery, Result<List<PartnerLabSummaryDto>>>
{
    public async Task<Result<List<PartnerLabSummaryDto>>> Handle(GetActivePartnerLabsQuery query, CancellationToken ct)
    {
        var labs = await db.PartnerLabs.AsNoTracking()
            .Where(l => l.IsActive)
            .OrderBy(l => l.LabName)
            .Select(l => new PartnerLabSummaryDto(
                l.Id,
                l.LabName,
                l.PhoneNumber,
                l.Address,
                l.IsActive
            ))
            .ToListAsync(ct);

        return Result<List<PartnerLabSummaryDto>>.Success(labs);
    }
}

