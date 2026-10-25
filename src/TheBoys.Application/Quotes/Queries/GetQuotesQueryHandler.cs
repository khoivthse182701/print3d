using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TheBoys.Application.Common.Interfaces;

namespace TheBoys.Application.Quotes.Queries;

public sealed class GetQuotesQueryHandler(
    IApplicationDbContext db,
    IClock clock) : IRequestHandler<GetQuotesQuery, IReadOnlyList<QuoteDto>>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task<IReadOnlyList<QuoteDto>> Handle(GetQuotesQuery request, CancellationToken ct)
    {
        var query = db.Quotes
            .Include(q => q.PricingPackage)
            .AsNoTracking();

        if (!request.IsManagement)
        {
            query = query.Where(q => q.UserId == request.UserId);
        }

        var quotes = await query
            .OrderByDescending(q => q.CreatedAtUtc)
            .Skip((Math.Max(1, request.Page) - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var now = clock.UtcNow;

        return quotes.Select(q =>
        {
            var address = JsonSerializer.Deserialize<ShippingAddressDto>(q.AddressJson, JsonOptions) ?? new ShippingAddressDto();
            return new QuoteDto(
                q.Id,
                q.UserId,
                q.VersionId,
                Convert.ToHexString(q.ConfigurationHash).ToLowerInvariant(),
                q.ApprovalId,
                q.ReviewId,
                q.PricingPackageId,
                q.PricingPackage.PackageCode,
                q.PricingPackage.Name,
                q.Quantity,
                q.UnitPriceVnd,
                q.ShippingFeeVnd,
                q.TotalVnd,
                address,
                q.SnapshotJson,
                q.ExpiresAtUtc,
                q.CreatedAtUtc,
                q.IsExpired(now)
            );
        }).ToList();
    }
}

