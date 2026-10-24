using TheBoys.Domain.Common;
using TheBoys.Domain.Enums;

namespace TheBoys.Domain.Entities;

public sealed class DefectReport : BaseEntity
{
    public Guid OrderId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid ClientRequestId { get; private set; }
    public string Reason { get; private set; } = default!;
    public DefectReportStatus Status { get; private set; } = DefectReportStatus.Open;
    public string? ResolutionNote { get; private set; }
    public Guid? ResolvedByUserId { get; private set; }
    public Guid? RefundId { get; set; }
    public Guid? ReprintRunId { get; set; }
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? ResolvedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    // Navigation
    public Order Order { get; private set; } = default!;
    public User User { get; private set; } = default!;
    public User? ResolvedByUser { get; private set; }
    public Refund? Refund { get; private set; }
    public ProductionRun? ReprintRun { get; private set; }
    public ICollection<DefectEvidence> Evidence { get; private set; } = new List<DefectEvidence>();

    private DefectReport() { }

    public static DefectReport Create(
        Guid orderId,
        Guid userId,
        Guid clientRequestId,
        string reason,
        DateTime createdAtUtc)
    {
        if (orderId == Guid.Empty) throw new ArgumentException("OrderId không được rỗng.", nameof(orderId));
        if (userId == Guid.Empty) throw new ArgumentException("UserId không được rỗng.", nameof(userId));
        if (clientRequestId == Guid.Empty) throw new ArgumentException("ClientRequestId không được rỗng.", nameof(clientRequestId));
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Reason không được rỗng.", nameof(reason));

        return new DefectReport
        {
            OrderId = orderId,
            UserId = userId,
            ClientRequestId = clientRequestId,
            Reason = reason.Trim(),
            Status = DefectReportStatus.Open,
            CreatedAtUtc = createdAtUtc
        };
    }

    public void ApproveReprint(Guid resolvedByUserId, Guid reprintRunId, string? resolutionNote, DateTime resolvedAtUtc)
    {
        if (resolvedByUserId == Guid.Empty) throw new ArgumentException("ResolvedByUserId không được rỗng.", nameof(resolvedByUserId));
        if (reprintRunId == Guid.Empty) throw new ArgumentException("ReprintRunId không được rỗng.", nameof(reprintRunId));

        Status = DefectReportStatus.ReprintApproved;
        ResolvedByUserId = resolvedByUserId;
        ReprintRunId = reprintRunId;
        RefundId = null;
        ResolutionNote = resolutionNote?.Trim();
        ResolvedAtUtc = resolvedAtUtc;
    }

    public void ApproveRefund(Guid resolvedByUserId, Guid? refundId, string? resolutionNote, DateTime resolvedAtUtc)
    {
        if (resolvedByUserId == Guid.Empty) throw new ArgumentException("ResolvedByUserId không được rỗng.", nameof(resolvedByUserId));

        Status = DefectReportStatus.RefundApproved;
        ResolvedByUserId = resolvedByUserId;
        RefundId = refundId;
        ReprintRunId = null;
        ResolutionNote = resolutionNote?.Trim();
        ResolvedAtUtc = resolvedAtUtc;
    }

    public void Reject(Guid resolvedByUserId, string? resolutionNote, DateTime resolvedAtUtc)
    {
        if (resolvedByUserId == Guid.Empty) throw new ArgumentException("ResolvedByUserId không được rỗng.", nameof(resolvedByUserId));

        Status = DefectReportStatus.Rejected;
        ResolvedByUserId = resolvedByUserId;
        ResolutionNote = resolutionNote?.Trim();
        ResolvedAtUtc = resolvedAtUtc;
    }

    public void Resolve(Guid resolvedByUserId, string? resolutionNote, DateTime resolvedAtUtc)
    {
        if (resolvedByUserId == Guid.Empty) throw new ArgumentException("ResolvedByUserId không được rỗng.", nameof(resolvedByUserId));

        Status = DefectReportStatus.Resolved;
        ResolvedByUserId = resolvedByUserId;
        ResolutionNote = resolutionNote?.Trim();
        ResolvedAtUtc = resolvedAtUtc;
    }
}

