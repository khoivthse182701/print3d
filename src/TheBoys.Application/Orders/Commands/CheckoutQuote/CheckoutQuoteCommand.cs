using MediatR;
using TheBoys.Application.Common.Models;
using TheBoys.Domain.Entities;

namespace TheBoys.Application.Orders.Commands.CheckoutQuote;

public sealed record CheckoutQuoteResult(Order Order, bool IsReplay);

public sealed record CheckoutQuoteCommand(
    Guid UserId,
    Guid QuoteId,
    string ClientRequestId,
    string RequestFingerprint
) : IRequest<Result<CheckoutQuoteResult>>;

