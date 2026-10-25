namespace TheBoys.Application.Common.Interfaces;

public interface IAuditService
{
    Task RecordAsync(
        Guid? actorUserId,
        string action,
        string aggregateType,
        Guid aggregateId,
        string traceId,
        object redactedChanges,
        string reason,
        CancellationToken ct);
}

