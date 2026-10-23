using MediatR;

namespace TheBoys.Application.Quotes.Queries;

public sealed record GetQuotesQuery(
    Guid UserId,
    bool IsManagement,
    int Page = 1,
    int PageSize = 20
) : IRequest<IReadOnlyList<QuoteDto>>;

