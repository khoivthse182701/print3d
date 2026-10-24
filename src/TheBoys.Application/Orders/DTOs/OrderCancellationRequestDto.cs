namespace TheBoys.Application.Orders.DTOs;

public sealed record OrderCancellationRequestDto(
    Guid Id,
    Guid OrderId,
    Guid RequestedByUserId,
    string Reason,
    string Status,
    Guid? DecidedByUserId,
    string? DecisionNote,
    Guid? RefundId,
    DateTime CreatedAtUtc,
    DateTime? DecidedAtUtc
);

