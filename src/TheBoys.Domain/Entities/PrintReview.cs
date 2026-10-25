namespace TheBoys.Domain.Entities;

public sealed class PrintReview
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid VersionId { get; private set; }
    public byte[] ConfigurationHash { get; private set; } = [];
    public Guid ReviewerUserId { get; private set; }
    public string Decision { get; private set; } = default!; // "Approved", "Rejected"
    public string ChecklistJson { get; private set; } = default!;
    public string? EvidenceStorageKey { get; private set; }
    public string Note { get; private set; } = default!;
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    // Navigation
    public ModelVersion Version { get; private set; } = default!;
    public User ReviewerUser { get; private set; } = default!;

    private PrintReview() { }

    public static PrintReview Create(
        Guid versionId,
        byte[] configurationHash,
        Guid reviewerUserId,
        string decision,
        string checklistJson,
        string? evidenceStorageKey,
        string note,
        DateTime createdAtUtc)
    {
        if (versionId == Guid.Empty)
            throw new ArgumentException("VersionId không được rỗng.", nameof(versionId));
        if (reviewerUserId == Guid.Empty)
            throw new ArgumentException("ReviewerUserId không được rỗng.", nameof(reviewerUserId));
        if (configurationHash == null || configurationHash.Length != 32)
            throw new ArgumentException("ConfigurationHash phải là SHA-256 32 bytes.", nameof(configurationHash));
        if (string.IsNullOrWhiteSpace(decision) || (decision != "Approved" && decision != "Rejected"))
            throw new ArgumentException("Decision phải là 'Approved' hoặc 'Rejected'.", nameof(decision));
        if (string.IsNullOrWhiteSpace(checklistJson))
            throw new ArgumentException("ChecklistJson không được rỗng.", nameof(checklistJson));
        if (string.IsNullOrWhiteSpace(note))
            throw new ArgumentException("Note/reason không được rỗng.", nameof(note));

        return new PrintReview
        {
            VersionId = versionId,
            ConfigurationHash = configurationHash,
            ReviewerUserId = reviewerUserId,
            Decision = decision,
            ChecklistJson = checklistJson,
            EvidenceStorageKey = evidenceStorageKey,
            Note = note.Trim(),
            CreatedAtUtc = createdAtUtc
        };
    }
}

