using TheBoys.Domain.Common;
using TheBoys.Domain.Enums;

namespace TheBoys.Domain.Entities;

public sealed class IdentityToken : BaseEntity
{
    public Guid UserId { get; private set; }
    public IdentityTokenPurpose Purpose { get; private set; }
    public byte[] TokenHash { get; private set; } = [];
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? ConsumedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    // Navigation
    public User? User { get; private set; }

    private IdentityToken() { }

    public static IdentityToken Create(
        Guid userId,
        IdentityTokenPurpose purpose,
        byte[] tokenHash,
        DateTime expiresAtUtc,
        DateTime utcNow)
    {
        if (tokenHash == null || tokenHash.Length == 0) throw new ArgumentException("TokenHash không được rỗng.", nameof(tokenHash));
        if (expiresAtUtc <= utcNow) throw new ArgumentException("ExpiresAtUtc phải lớn hơn CreatedAtUtc.", nameof(expiresAtUtc));

        return new IdentityToken
        {
            UserId = userId,
            Purpose = purpose,
            TokenHash = tokenHash,
            ExpiresAtUtc = expiresAtUtc,
            CreatedAtUtc = utcNow
        };
    }

    public bool IsValid(DateTime utcNow) => ConsumedAtUtc == null && ExpiresAtUtc > utcNow;

    public void Consume(DateTime utcNow)
    {
        if (!IsValid(utcNow)) throw new InvalidOperationException("Token không hợp lệ hoặc đã hết hạn.");
        ConsumedAtUtc = utcNow;
    }
}

