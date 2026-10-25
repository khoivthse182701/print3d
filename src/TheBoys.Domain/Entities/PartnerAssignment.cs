using TheBoys.Domain.Common;
using TheBoys.Domain.Enums;

namespace TheBoys.Domain.Entities;

public sealed class PartnerAssignment : BaseEntity
{
    public Guid OrderId { get; private set; }
    public Guid PartnerLabId { get; private set; }
    public Guid AssignedByUserId { get; private set; }
    public PartnerAssignmentStatus Status { get; private set; } = PartnerAssignmentStatus.Offered;
    public bool IsActive { get; private set; } = true;
    public decimal PartnerCostSnapshotVnd { get; private set; }
    public DateTime DueAtUtc { get; private set; }
    public string? DecisionNote { get; private set; }
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    // Navigation
    public Order Order { get; private set; } = default!;
    public PartnerLab PartnerLab { get; private set; } = default!;
    public User AssignedByUser { get; private set; } = default!;
    public ICollection<ProductionRun> ProductionRuns { get; private set; } = new List<ProductionRun>();

    private PartnerAssignment() { }

    public static PartnerAssignment Create(
        Guid orderId,
        Guid partnerLabId,
        Guid assignedByUserId,
        decimal partnerCostSnapshotVnd,
        DateTime dueAtUtc,
        string? decisionNote,
        DateTime createdAtUtc)
    {
        if (orderId == Guid.Empty) throw new ArgumentException("OrderId không được rỗng.", nameof(orderId));
        if (partnerLabId == Guid.Empty) throw new ArgumentException("PartnerLabId không được rỗng.", nameof(partnerLabId));
        if (assignedByUserId == Guid.Empty) throw new ArgumentException("AssignedByUserId không được rỗng.", nameof(assignedByUserId));
        if (partnerCostSnapshotVnd < 0) throw new ArgumentException("PartnerCostSnapshotVnd phải >= 0.", nameof(partnerCostSnapshotVnd));

        return new PartnerAssignment
        {
            OrderId = orderId,
            PartnerLabId = partnerLabId,
            AssignedByUserId = assignedByUserId,
            Status = PartnerAssignmentStatus.Offered,
            IsActive = true,
            PartnerCostSnapshotVnd = partnerCostSnapshotVnd,
            DueAtUtc = dueAtUtc,
            DecisionNote = decisionNote?.Trim(),
            CreatedAtUtc = createdAtUtc
        };
    }

    public void Accept(string? decisionNote = null)
    {
        Status = PartnerAssignmentStatus.Accepted;
        IsActive = true;
        if (!string.IsNullOrWhiteSpace(decisionNote))
        {
            DecisionNote = decisionNote.Trim();
        }
    }

    public void Reject(string? decisionNote = null)
    {
        Status = PartnerAssignmentStatus.Rejected;
        IsActive = false;
        if (!string.IsNullOrWhiteSpace(decisionNote))
        {
            DecisionNote = decisionNote.Trim();
        }
    }

    public void Expire()
    {
        Status = PartnerAssignmentStatus.Expired;
        IsActive = false;
    }

    public void Complete(string? decisionNote = null)
    {
        Status = PartnerAssignmentStatus.Completed;
        IsActive = false;
        if (!string.IsNullOrWhiteSpace(decisionNote))
        {
            DecisionNote = decisionNote.Trim();
        }
    }

    public void Cancel(string? decisionNote = null)
    {
        Status = PartnerAssignmentStatus.Cancelled;
        IsActive = false;
        if (!string.IsNullOrWhiteSpace(decisionNote))
        {
            DecisionNote = decisionNote.Trim();
        }
    }
}

