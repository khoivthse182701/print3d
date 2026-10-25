using TheBoys.Domain.Common;

namespace TheBoys.Domain.Entities;

public sealed class ConsentRecord : BaseEntity
{
    public Guid UserId { get; private set; }
    public string Purpose { get; private set; } = default!;
    public string PolicyVersion { get; private set; } = default!;
    public bool Granted { get; private set; }
    public DateTime RecordedAtUtc { get; private set; }

    // Navigation
    public User? User { get; private set; }

    private ConsentRecord() { }

    public static ConsentRecord Record(
        Guid userId,
        string purpose,
        string policyVersion,
        bool granted,
        DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(purpose)) throw new ArgumentException("Purpose không được rỗng.", nameof(purpose));
        if (string.IsNullOrWhiteSpace(policyVersion)) throw new ArgumentException("PolicyVersion không được rỗng.", nameof(policyVersion));

        return new ConsentRecord
        {
            UserId = userId,
            Purpose = purpose.Trim(),
            PolicyVersion = policyVersion.Trim(),
            Granted = granted,
            RecordedAtUtc = utcNow
        };
    }
}

