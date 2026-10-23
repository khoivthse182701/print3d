namespace TheBoys.Domain.Entities;

public sealed class GenerationStage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid JobId { get; set; }
    public string Stage { get; set; } = "Generate"; // Generate, Resize, Convert, Analyze, Repair
    public int AttemptNo { get; set; } = 1;
    public string Provider { get; set; } = "Meshy";
    public string ProviderAccountScope { get; set; } = "default";
    public string? ProviderTaskId { get; set; }
    public string Status { get; set; } = "Queued"; // Queued, Submitting, Running, Succeeded, Failed, NeedsAttention
    public int Progress { get; set; }
    public bool IsActive { get; set; } = true;
    public bool SubmissionPending { get; set; }
    public int PollFailureCount { get; set; }
    public DateTime? NextPollAtUtc { get; set; }
    public string? LeaseOwner { get; set; }
    public DateTime? LeaseExpiresAtUtc { get; set; }
    public decimal? ProviderCredits { get; set; }
    public decimal? ActualCostVnd { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];

    // Navigation
    public GenerationJob Job { get; set; } = default!;
}

