using MediatR;
using TheBoys.Application.Common.Models;
using TheBoys.Application.Payments.DTOs;

namespace TheBoys.Application.Payments.Commands.CreatePaymentAttempt;

public sealed record CreatePaymentAttemptResult(PaymentAttemptDto Attempt, bool IsReplay);

public sealed record CreatePaymentAttemptCommand(
    Guid UserId,
    Guid OrderId,
    string ClientRequestId,
    string RequestFingerprint,
    bool IsManagement = false
) : IRequest<Result<CreatePaymentAttemptResult>>;

