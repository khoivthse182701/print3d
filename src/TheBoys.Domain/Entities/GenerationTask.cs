using TheBoys.Domain.Common;
using TheBoys.Domain.Enums;

namespace TheBoys.Domain.Entities;

public sealed class GenerationTask : BaseAuditableEntity
{
    public Guid UserId { get; set; }
    public TaskType TaskType { get; set; }
    public string? Prompt { get; set; }
    public string? ArtStyle { get; set; }
    public string? InputImageUrl { get; set; }
    public string? CleanedImageUrl { get; set; }
    public GenerationStatus Status { get; set; } = GenerationStatus.Queued;
    public string? ProviderTaskId { get; set; }
    public string Provider { get; set; } = "Meshy";
    public string AiModel { get; set; } = "meshy-6-lite";
    public bool ShouldTexture { get; set; }
    public int RequestedHeightMm { get; set; } = 100;
    public string PrinterType { get; set; } = "sla";
    public int Progress { get; set; }
    public GenerationStage Stage { get; set; }
    public string? ResizeTaskId { get; set; }
    public string? ConvertTaskId { get; set; }
    public string? AnalysisTaskId { get; set; }
    public string? ResizedModelUrl { get; set; }
    public bool SubmissionPending { get; set; }
    public DateTime? NextPollAtUtc { get; set; }
    public string ClientRequestId { get; set; } = Guid.NewGuid().ToString("N");
    public string RequestFingerprint { get; set; } = "";
    public bool ChargedPurchasedCredit { get; set; }
    public bool CreditRefunded { get; set; }
    public string? PrintabilityStatus { get; set; }
    public string? PrintabilityJson { get; set; }
    public PrintReviewStatus ReviewStatus { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public string? ReviewNote { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public int? DurationSeconds { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    // Gộp từ ModelAssets: Thông số hình học 3D & đường dẫn file
    public string? ModelName { get; set; }
    public string? StoragePathGlb { get; set; }
    public string? StoragePathStl { get; set; }
    public string? ThumbnailUrl { get; set; }
    public int? Polycount { get; set; }
    public int? VertexCount { get; set; }
    public bool IsWatertight { get; set; }
    public decimal? MinWallThicknessMm { get; set; }
    public decimal? BoundingBoxX_Mm { get; set; }
    public decimal? BoundingBoxY_Mm { get; set; }
    public decimal? BoundingBoxZ_Mm { get; set; }
    public decimal? EstimatedVolumeCm3 { get; set; }
    public bool IsApprovedForPrinting { get; set; }

    // Navigation
    public User User { get; set; } = default!;
}
