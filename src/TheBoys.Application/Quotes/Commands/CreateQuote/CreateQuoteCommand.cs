using MediatR;
using TheBoys.Application.Common.Models;

namespace TheBoys.Application.Quotes.Commands.CreateQuote;

public sealed record CreateQuoteCommand(
    Guid UserId,
    Guid VersionId,
    object Configuration,
    Guid PricingPackageId,
    int Quantity,
    ShippingAddressDto Address
) : IRequest<Result<QuoteDto>>;

