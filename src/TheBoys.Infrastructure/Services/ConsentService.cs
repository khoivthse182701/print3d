using Microsoft.EntityFrameworkCore;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Domain.Entities;

namespace TheBoys.Infrastructure.Services;

public sealed class ConsentService(IApplicationDbContext db, IClock clock) : IConsentService
{
    public async Task<bool> HasValidConsentAsync(Guid userId, string purpose, string policyVersion, CancellationToken ct)
    {
        var latest = await db.ConsentRecords.AsNoTracking()
            .Where(x => x.UserId == userId && x.Purpose == purpose)
            .OrderByDescending(x => x.RecordedAtUtc)
            .FirstOrDefaultAsync(ct);

        return latest != null && latest.Granted && latest.PolicyVersion == policyVersion;
    }

    public async Task GrantConsentAsync(Guid userId, string purpose, string policyVersion, CancellationToken ct)
    {
        var record = ConsentRecord.Record(userId, purpose, policyVersion, granted: true, clock.UtcNow);
        db.ConsentRecords.Add(record);
        await db.SaveChangesAsync(ct);
    }

    public async Task RevokeConsentAsync(Guid userId, string purpose, string policyVersion, CancellationToken ct)
    {
        var record = ConsentRecord.Record(userId, purpose, policyVersion, granted: false, clock.UtcNow);
        db.ConsentRecords.Add(record);
        await db.SaveChangesAsync(ct);
    }
}

