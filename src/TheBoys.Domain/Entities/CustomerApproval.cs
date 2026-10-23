namespace TheBoys.Domain.Entities;

public sealed class CustomerApproval
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public Guid VersionId { get; private set; }
    public byte[] ConfigurationHash { get; private set; } = [];
    public string ConfigurationJson { get; private set; } = default!;
    public string PolicyVersion { get; private set; } = "v1.0";
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    // Navigation
    public User User { get; private set; } = default!;
    public ModelVersion Version { get; private set; } = default!;

    private CustomerApproval() { }

    public static CustomerApproval Create(
        Guid userId,
        Guid versionId,
        byte[] configurationHash,
        string configurationJson,
        string policyVersion,
        DateTime createdAtUtc)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId không được rỗng.", nameof(userId));
        if (versionId == Guid.Empty)
            throw new ArgumentException("VersionId không được rỗng.", nameof(versionId));
        if (configurationHash == null || configurationHash.Length != 32)
            throw new ArgumentException("ConfigurationHash phải là SHA-256 32 bytes.", nameof(configurationHash));
        if (string.IsNullOrWhiteSpace(configurationJson))
            throw new ArgumentException("ConfigurationJson không được rỗng.", nameof(configurationJson));
        if (string.IsNullOrWhiteSpace(policyVersion))
            throw new ArgumentException("PolicyVersion không được rỗng.", nameof(policyVersion));

        return new CustomerApproval
        {
            UserId = userId,
            VersionId = versionId,
            ConfigurationHash = configurationHash,
            ConfigurationJson = configurationJson,
            PolicyVersion = policyVersion.Trim(),
            CreatedAtUtc = createdAtUtc
        };
    }
}

