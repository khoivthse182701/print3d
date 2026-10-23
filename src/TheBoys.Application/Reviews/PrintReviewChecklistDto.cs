namespace TheBoys.Application.Reviews;

public sealed class PrintReviewChecklistDto
{
    public string Orientation { get; set; } = "default";
    public bool SupportConfirmation { get; set; }
    public string Material { get; set; } = "PLA";
    public decimal PrintHeightMm { get; set; }
    public bool BuildVolumeConfirmation { get; set; }
    public string? SlicingSoftware { get; set; }
    public string? ExceptionReason { get; set; }
}

