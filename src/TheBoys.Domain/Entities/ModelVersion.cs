namespace TheBoys.Domain.Entities;

public sealed class ModelVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Guid? SourceJobId { get; set; }
    public Guid? ParentVersionId { get; set; }
    public int VersionNo { get; set; } = 1;
    public byte[] GeometryHash { get; set; } = [];
    public decimal WidthMm { get; set; }
    public decimal DepthMm { get; set; }
    public decimal HeightMm { get; set; }
    public decimal? VolumeCm3 { get; set; }
    public bool IsWatertight { get; set; }
    public string PrintabilityStatus { get; set; } = "Unknown";
    public string AnalysisCoverage { get; set; } = "GeometryOnly"; // GeometryOnly, GeometryAndThickness, ManualSlicer
    public string? AnalysisJson { get; set; }
    public bool IsPublished { get; set; } = true;
    public DateTime PublishedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation
    public ModelProject Project { get; set; } = default!;
    public GenerationJob? SourceJob { get; set; }
    public ModelVersion? ParentVersion { get; set; }
    public ICollection<ModelVersion> ChildVersions { get; set; } = new List<ModelVersion>();
    public ICollection<ModelAsset> Assets { get; set; } = new List<ModelAsset>();
    public ICollection<PrintReview> Reviews { get; set; } = new List<PrintReview>();
    public ICollection<CustomerApproval> CustomerApprovals { get; set; } = new List<CustomerApproval>();
    public ICollection<Quote> Quotes { get; set; } = new List<Quote>();
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
