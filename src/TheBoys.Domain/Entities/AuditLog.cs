using TheBoys.Domain.Common;

namespace TheBoys.Domain.Entities;

public sealed class AuditLog : BaseEntity
{
    public Guid? ActorUserId { get; private set; }
    public string Action { get; private set; } = default!;
    public string AggregateType { get; private set; } = default!;
    public Guid AggregateId { get; private set; }
    public string TraceId { get; private set; } = default!;
    public string RedactedChangesJson { get; private set; } = "{}";
    public string Reason { get; private set; } = default!;
    public DateTime CreatedAtUtc { get; private set; }

    // Navigation
    public User? ActorUser { get; private set; }

    private AuditLog() { }

    public static AuditLog Create(
        Guid? actorUserId,
        string action,
        string aggregateType,
        Guid aggregateId,
        string traceId,
        string redactedChangesJson,
        string reason,
        DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(action)) throw new ArgumentException("Action không được rỗng.", nameof(action));
        if (string.IsNullOrWhiteSpace(aggregateType)) throw new ArgumentException("AggregateType không được rỗng.", nameof(aggregateType));
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Reason không được rỗng.", nameof(reason));

        return new AuditLog
        {
            ActorUserId = actorUserId,
            Action = action.Trim(),
            AggregateType = aggregateType.Trim(),
            AggregateId = aggregateId,
            TraceId = string.IsNullOrWhiteSpace(traceId) ? Guid.NewGuid().ToString("N") : traceId.Trim(),
            RedactedChangesJson = string.IsNullOrWhiteSpace(redactedChangesJson) ? "{}" : redactedChangesJson.Trim(),
            Reason = reason.Trim(),
            CreatedAtUtc = utcNow
        };
    }
}

