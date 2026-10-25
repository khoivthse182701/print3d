using TheBoys.Domain.Common;
using TheBoys.Domain.Enums;

namespace TheBoys.Domain.Entities;

public sealed class Notification : BaseEntity
{
    public Guid UserId { get; private set; }
    public Guid OutboxMessageId { get; private set; }
    public NotificationChannel Channel { get; private set; } = NotificationChannel.InApp;
    public string Title { get; private set; } = default!;
    public string Body { get; private set; } = default!;
    public DateTime? DeliveredAtUtc { get; private set; }
    public DateTime? ReadAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    // Navigation
    public User User { get; private set; } = default!;
    public OutboxMessage OutboxMessage { get; private set; } = default!;

    private Notification() { }

    public static Notification Create(
        Guid userId,
        Guid outboxMessageId,
        NotificationChannel channel,
        string title,
        string body,
        DateTime utcNow)
    {
        if (userId == Guid.Empty) throw new ArgumentException("UserId không được rỗng.", nameof(userId));
        if (outboxMessageId == Guid.Empty) throw new ArgumentException("OutboxMessageId không được rỗng.", nameof(outboxMessageId));
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title không được rỗng.", nameof(title));
        if (string.IsNullOrWhiteSpace(body)) throw new ArgumentException("Body không được rỗng.", nameof(body));

        return new Notification
        {
            UserId = userId,
            OutboxMessageId = outboxMessageId,
            Channel = channel,
            Title = title.Trim(),
            Body = body.Trim(),
            CreatedAtUtc = utcNow
        };
    }

    public void MarkDelivered(DateTime utcNow)
    {
        DeliveredAtUtc = utcNow;
    }

    public void MarkRead(DateTime utcNow)
    {
        ReadAtUtc = utcNow;
    }
}

