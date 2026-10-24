namespace TheBoys.Application.Payments.DTOs;

public sealed record RefundDto(
    Guid Id,
    Guid PaymentAttemptId,
    Guid RequestedByUserId,
    Guid? ApprovedByUserId,
    Guid OperationKey,
    string? ProviderReference,
    decimal AmountVnd,
    string Status,
    string Reason,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc
);

