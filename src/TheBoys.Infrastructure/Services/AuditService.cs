using System.Text.Json;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Domain.Entities;

namespace TheBoys.Infrastructure.Services;

public sealed class AuditService(IApplicationDbContext db, IClock clock) : IAuditService
{
    public async Task RecordAsync(
        Guid? actorUserId,
        string action,
        string aggregateType,
        Guid aggregateId,
        string traceId,
        object redactedChanges,
        string reason,
        CancellationToken ct)
    {
        var json = redactedChanges is string str ? str : JsonSerializer.Serialize(redactedChanges);
        var audit = AuditLog.Create(
            actorUserId,
            action,
            aggregateType,
            aggregateId,
            traceId,
            json,
            reason,
            clock.UtcNow
        );

        db.AuditLogs.Add(audit);
        await db.SaveChangesAsync(ct);
    }
}

