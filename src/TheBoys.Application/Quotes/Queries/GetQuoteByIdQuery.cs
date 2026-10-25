using MediatR;
using TheBoys.Application.Common.Models;

namespace TheBoys.Application.Quotes.Queries;

public sealed record GetQuoteByIdQuery(
    Guid QuoteId,
    Guid CurrentUserId,
    bool IsManagement
) : IRequest<Result<QuoteDto>>;

