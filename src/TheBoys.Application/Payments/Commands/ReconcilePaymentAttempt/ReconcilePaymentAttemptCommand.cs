using MediatR;
using TheBoys.Application.Common.Models;
using TheBoys.Application.Payments.DTOs;

namespace TheBoys.Application.Payments.Commands.ReconcilePaymentAttempt;

public sealed record ReconcilePaymentAttemptResult(PaymentAttemptDto Attempt, string Message);

public sealed record ReconcilePaymentAttemptCommand(
    Guid PaymentAttemptId,
    Guid StaffUserId
) : IRequest<Result<ReconcilePaymentAttemptResult>>;

