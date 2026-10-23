using TheBoys.Domain.Common;

namespace TheBoys.Domain.Entities;

public sealed class OutboxMessage : BaseEntity
{
    public string EventType { get; private set; } = default!;
    public Guid AggregateId { get; private set; }
    public string DeduplicationKey { get; private set; } = default!;
    public string PayloadJson { get; private set; } = "{}";
    public int AttemptCount { get; private set; }
    public DateTime AvailableAtUtc { get; private set; }
    public string? LeaseOwner { get; private set; }
    public DateTime? LeaseExpiresAtUtc { get; private set; }
    public DateTime? ProcessedAtUtc { get; private set; }
    public DateTime? DeadLetteredAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private OutboxMessage() { }

    public static OutboxMessage Create(
        string eventType,
        Guid aggregateId,
        string deduplicationKey,
        string payloadJson,
        DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(eventType)) throw new ArgumentException("EventType không được rỗng.", nameof(eventType));
        if (string.IsNullOrWhiteSpace(deduplicationKey)) throw new ArgumentException("DeduplicationKey không được rỗng.", nameof(deduplicationKey));

        return new OutboxMessage
        {
            EventType = eventType.Trim(),
            AggregateId = aggregateId,
            DeduplicationKey = deduplicationKey.Trim(),
            PayloadJson = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson.Trim(),
            AvailableAtUtc = utcNow,
            CreatedAtUtc = utcNow
        };
    }

    public void MarkProcessed(DateTime utcNow)
    {
        ProcessedAtUtc = utcNow;
        LeaseOwner = null;
        LeaseExpiresAtUtc = null;
    }
}

