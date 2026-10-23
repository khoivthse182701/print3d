using MediatR;
using TheBoys.Application.Common.Models;

namespace TheBoys.Application.Payments.Commands.ProcessPaymentWebhook;

public sealed record ProcessPaymentWebhookResult(bool Acknowledged, string Message, string? ProcessingResult = null);

public sealed record ProcessPaymentWebhookCommand(
    string Gateway,
    byte[] RawBody,
    IReadOnlyDictionary<string, string> Headers
) : IRequest<Result<ProcessPaymentWebhookResult>>;

