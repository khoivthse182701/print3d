namespace TheBoys.Domain.Entities;

public sealed class GenerationJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Guid UserId { get; set; }
    public string Operation { get; set; } = "ImageTo3D";
    public string Provider { get; set; } = "Meshy";
    public string ProviderModel { get; set; } = "meshy-6-lite";
    public string ApiVersion { get; set; } = "v1";
    public string PolicyVersion { get; set; } = "v1.0";
    public string PricingVersion { get; set; } = "2026.01";
    public string AccountScope { get; set; } = "default";
    public string? Prompt { get; set; }
    public string? InputStorageKey { get; set; }
    public byte[] InputHash { get; set; } = [];
    public Guid? InputConsentId { get; set; }
    public decimal RequestedHeightMm { get; set; } = 100m;
    public bool ShouldTexture { get; set; }
    public string Status { get; set; } = "Queued";
    public Guid ClientRequestId { get; set; }
    public byte[] RequestFingerprint { get; set; } = [];
    public string? ErrorMessage { get; set; }
    public Guid? SourceVersionId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];

    // Navigation
    public ModelProject Project { get; set; } = default!;
    public User User { get; set; } = default!;
    public ConsentRecord? InputConsent { get; set; }
    public ModelVersion? SourceVersion { get; set; }
    public ICollection<GenerationStage> Stages { get; set; } = new List<GenerationStage>();
    public ICollection<ModelVersion> ProducedVersions { get; set; } = new List<ModelVersion>();
}

