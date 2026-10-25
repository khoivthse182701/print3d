namespace TheBoys.Application.Notifications.DTOs;

public sealed record NotificationDto(
    Guid Id,
    Guid UserId,
    Guid OutboxMessageId,
    string Channel,
    string Title,
    string Body,
    DateTime? DeliveredAtUtc,
    DateTime? ReadAtUtc,
    DateTime CreatedAtUtc
);

