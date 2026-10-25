using TheBoys.Domain.Common;

namespace TheBoys.Domain.Entities;

public sealed class RefreshSession : BaseEntity
{
    public Guid UserId { get; private set; }
    public Guid FamilyId { get; private set; }
    public byte[] TokenHash { get; private set; } = [];
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public Guid? ReplacedById { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    // Navigations
    public User? User { get; private set; }
    public RefreshSession? ReplacedBy { get; private set; }

    private RefreshSession() { }

    public static RefreshSession Create(
        Guid userId,
        Guid familyId,
        byte[] tokenHash,
        DateTime expiresAtUtc,
        DateTime utcNow)
    {
        if (tokenHash == null || tokenHash.Length == 0) throw new ArgumentException("TokenHash không được rỗng.", nameof(tokenHash));
        if (expiresAtUtc <= utcNow) throw new ArgumentException("ExpiresAtUtc phải lớn hơn CreatedAtUtc.", nameof(expiresAtUtc));

        return new RefreshSession
        {
            UserId = userId,
            FamilyId = familyId,
            TokenHash = tokenHash,
            ExpiresAtUtc = expiresAtUtc,
            CreatedAtUtc = utcNow
        };
    }

    public bool IsActive(DateTime utcNow) => RevokedAtUtc == null && ExpiresAtUtc > utcNow;

    public void Revoke(DateTime utcNow)
    {
        RevokedAtUtc = utcNow;
    }

    public void Replace(Guid newSessionId, DateTime utcNow)
    {
        ReplacedById = newSessionId;
        RevokedAtUtc = utcNow;
    }
}

