using TheBoys.Domain.Common;

namespace TheBoys.Domain.Entities;

public sealed class DefectEvidence : BaseEntity
{
    public Guid ReportId { get; private set; }
    public string StorageKey { get; private set; } = default!;
    public byte[] Sha256 { get; private set; } = Array.Empty<byte>();
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    // Navigation
    public DefectReport Report { get; private set; } = default!;

    private DefectEvidence() { }

    public static DefectEvidence Create(
        Guid reportId,
        string storageKey,
        byte[] sha256,
        DateTime createdAtUtc)
    {
        if (reportId == Guid.Empty) throw new ArgumentException("ReportId không được rỗng.", nameof(reportId));
        if (string.IsNullOrWhiteSpace(storageKey)) throw new ArgumentException("StorageKey không được rỗng.", nameof(storageKey));
        if (sha256 == null || sha256.Length != 32)
            throw new ArgumentException("Sha256 phải là mảng 32 bytes.", nameof(sha256));

        return new DefectEvidence
        {
            ReportId = reportId,
            StorageKey = storageKey.Trim(),
            Sha256 = sha256,
            CreatedAtUtc = createdAtUtc
        };
    }
}

