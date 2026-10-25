using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Application.Common.Models;

namespace TheBoys.Application.Quotes.Queries;

public sealed class GetQuoteByIdQueryHandler(
    IApplicationDbContext db,
    IClock clock) : IRequestHandler<GetQuoteByIdQuery, Result<QuoteDto>>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task<Result<QuoteDto>> Handle(GetQuoteByIdQuery request, CancellationToken ct)
    {
        var quote = await db.Quotes
            .Include(q => q.PricingPackage)
            .SingleOrDefaultAsync(q => q.Id == request.QuoteId, ct);

        if (quote == null)
            return Result<QuoteDto>.Failure(Error.NotFound("QuoteNotFound", "Không tìm thấy báo giá."));

        if (!request.IsManagement && quote.UserId != request.CurrentUserId)
            return Result<QuoteDto>.Failure(Error.Unauthorized("NotAuthorized", "Không có quyền xem báo giá của người khác."));

        var address = JsonSerializer.Deserialize<ShippingAddressDto>(quote.AddressJson, JsonOptions) ?? new ShippingAddressDto();

        return Result<QuoteDto>.Success(new QuoteDto(
            quote.Id,
            quote.UserId,
            quote.VersionId,
            Convert.ToHexString(quote.ConfigurationHash).ToLowerInvariant(),
            quote.ApprovalId,
            quote.ReviewId,
            quote.PricingPackageId,
            quote.PricingPackage.PackageCode,
            quote.PricingPackage.Name,
            quote.Quantity,
            quote.UnitPriceVnd,
            quote.ShippingFeeVnd,
            quote.TotalVnd,
            address,
            quote.SnapshotJson,
            quote.ExpiresAtUtc,
            quote.CreatedAtUtc,
            quote.IsExpired(clock.UtcNow)
        ));
    }
}

