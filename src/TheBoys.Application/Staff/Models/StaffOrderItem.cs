using TheBoys.Domain.Enums;

namespace TheBoys.Application.Staff.Models;

public sealed record StaffOrderItem(
    Guid Id,
    string OrderCode,
    Guid? GenerationTaskId,
    OrderStatus Status,
    int Quantity,
    DateTime CreatedAtUtc,
    long AgeMinutes,
    DateTime? PaidAtUtc,
    DateTime? SentToPartnerAtUtc,
    DateTime? PrintingStartedAtUtc,
    DateTime? PackagedAtUtc,
    DateTime? ShippedAtUtc,
    DateTime? DeliveredAtUtc,
    DateTime? CancelledAtUtc
);
