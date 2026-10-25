using TheBoys.Domain.Common;
using TheBoys.Domain.Enums;

namespace TheBoys.Domain.Entities;

public sealed class ProductionRun : BaseEntity
{
    public Guid AssignmentId { get; private set; }
    public int RunNo { get; private set; }
    public ProductionRunStatus Status { get; private set; } = ProductionRunStatus.Printing;
    public int Quantity { get; private set; }
    public decimal? ActualCostVnd { get; private set; }
    public string? QcEvidenceStorageKey { get; private set; }
    public string? QcNote { get; private set; }
    public DateTime StartedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    // Navigation
    public PartnerAssignment Assignment { get; private set; } = default!;
    public DefectReport? DefectReportReprint { get; private set; }

    private ProductionRun() { }

    public static ProductionRun Start(
        Guid assignmentId,
        int runNo,
        int quantity,
        DateTime startedAtUtc,
        decimal? actualCostVnd = null)
    {
        if (assignmentId == Guid.Empty) throw new ArgumentException("AssignmentId không được rỗng.", nameof(assignmentId));
        if (runNo <= 0) throw new ArgumentException("RunNo phải > 0.", nameof(runNo));
        if (quantity <= 0) throw new ArgumentException("Quantity phải > 0.", nameof(quantity));
        if (actualCostVnd.HasValue && actualCostVnd.Value < 0)
            throw new ArgumentException("ActualCostVnd phải >= 0 nếu có giá trị.", nameof(actualCostVnd));

        return new ProductionRun
        {
            AssignmentId = assignmentId,
            RunNo = runNo,
            Quantity = quantity,
            Status = ProductionRunStatus.Printing,
            ActualCostVnd = actualCostVnd,
            StartedAtUtc = startedAtUtc
        };
    }

    public void AdvanceToPostProcessing()
    {
        Status = ProductionRunStatus.PostProcessing;
    }

    public void AdvanceToQualityCheck()
    {
        Status = ProductionRunStatus.QualityCheck;
    }

    public void MarkPassed(string qcEvidenceStorageKey, string qcNote, DateTime completedAtUtc, decimal? actualCostVnd = null)
    {
        if (string.IsNullOrWhiteSpace(qcEvidenceStorageKey))
            throw new ArgumentException("QcEvidenceStorageKey không được rỗng khi QC Passed.", nameof(qcEvidenceStorageKey));
        if (string.IsNullOrWhiteSpace(qcNote))
            throw new ArgumentException("QcNote không được rỗng khi QC Passed.", nameof(qcNote));

        Status = ProductionRunStatus.Passed;
        QcEvidenceStorageKey = qcEvidenceStorageKey.Trim();
        QcNote = qcNote.Trim();
        CompletedAtUtc = completedAtUtc;
        if (actualCostVnd.HasValue && actualCostVnd.Value >= 0)
        {
            ActualCostVnd = actualCostVnd.Value;
        }
    }

    public void MarkFailed(string qcNote, string? qcEvidenceStorageKey, DateTime completedAtUtc, decimal? actualCostVnd = null)
    {
        if (string.IsNullOrWhiteSpace(qcNote))
            throw new ArgumentException("QcNote không được rỗng khi QC Failed.", nameof(qcNote));

        Status = ProductionRunStatus.Failed;
        QcNote = qcNote.Trim();
        QcEvidenceStorageKey = qcEvidenceStorageKey?.Trim();
        CompletedAtUtc = completedAtUtc;
        if (actualCostVnd.HasValue && actualCostVnd.Value >= 0)
        {
            ActualCostVnd = actualCostVnd.Value;
        }
    }

    public void Cancel()
    {
        Status = ProductionRunStatus.Cancelled;
    }
}

