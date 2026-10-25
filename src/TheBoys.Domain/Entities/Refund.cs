using TheBoys.Domain.Common;
using TheBoys.Domain.Enums;

namespace TheBoys.Domain.Entities;

public sealed class Refund : BaseEntity
{
    public Guid PaymentAttemptId { get; private set; }
    public Guid RequestedByUserId { get; private set; }
    public Guid? ApprovedByUserId { get; private set; }
    public Guid OperationKey { get; private set; }
    public string? ProviderReference { get; private set; }
    public decimal AmountVnd { get; private set; }
    public RefundStatus Status { get; private set; } = RefundStatus.Requested;
    public string Reason { get; private set; } = default!;
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    // Navigation
    public PaymentAttempt PaymentAttempt { get; private set; } = default!;
    public User RequestedByUser { get; private set; } = default!;
    public User? ApprovedByUser { get; private set; }
    public ICollection<OrderCancellationRequest> CancellationRequests { get; private set; } = new List<OrderCancellationRequest>();
    public ICollection<DefectReport> DefectReports { get; private set; } = new List<DefectReport>();

    private Refund() { }

    public static Refund Create(
        Guid paymentAttemptId,
        Guid requestedByUserId,
        Guid operationKey,
        decimal amountVnd,
        string reason,
        DateTime createdAtUtc)
    {
        if (paymentAttemptId == Guid.Empty) throw new ArgumentException("PaymentAttemptId không được rỗng.", nameof(paymentAttemptId));
        if (requestedByUserId == Guid.Empty) throw new ArgumentException("RequestedByUserId không được rỗng.", nameof(requestedByUserId));
        if (operationKey == Guid.Empty) throw new ArgumentException("OperationKey không được rỗng.", nameof(operationKey));
        if (amountVnd <= 0) throw new ArgumentException("AmountVnd phải lớn hơn 0.", nameof(amountVnd));
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Reason không được rỗng.", nameof(reason));

        return new Refund
        {
            PaymentAttemptId = paymentAttemptId,
            RequestedByUserId = requestedByUserId,
            OperationKey = operationKey,
            AmountVnd = amountVnd,
            Reason = reason.Trim(),
            Status = RefundStatus.Requested,
            CreatedAtUtc = createdAtUtc
        };
    }

    public void MarkReserved()
    {
        Status = RefundStatus.Reserved;
    }

    public void MarkSubmitted(string? providerReference = null)
    {
        Status = RefundStatus.Submitted;
        if (!string.IsNullOrWhiteSpace(providerReference))
        {
            ProviderReference = providerReference.Trim();
        }
    }

    public void MarkSucceeded(string? providerReference, DateTime completedAtUtc, Guid? approvedByUserId = null)
    {
        Status = RefundStatus.Succeeded;
        if (!string.IsNullOrWhiteSpace(providerReference))
        {
            ProviderReference = providerReference.Trim();
        }
        CompletedAtUtc = completedAtUtc;
        if (approvedByUserId.HasValue && approvedByUserId.Value != Guid.Empty)
        {
            ApprovedByUserId = approvedByUserId.Value;
        }
    }

    public void MarkFailed(string? failureReason)
    {
        Status = RefundStatus.Failed;
        if (!string.IsNullOrWhiteSpace(failureReason))
        {
            Reason = $"{Reason} | Failed: {failureReason.Trim()}";
            if (Reason.Length > 1000) Reason = Reason[..1000];
        }
    }

    public void MarkNeedsAttention(string? reason)
    {
        Status = RefundStatus.NeedsAttention;
        if (!string.IsNullOrWhiteSpace(reason))
        {
            Reason = $"{Reason} | Attention: {reason.Trim()}";
            if (Reason.Length > 1000) Reason = Reason[..1000];
        }
    }

    public void MarkRejected(Guid decidedByUserId, string? note = null)
    {
        Status = RefundStatus.Rejected;
        ApprovedByUserId = decidedByUserId;
        if (!string.IsNullOrWhiteSpace(note))
        {
            Reason = $"{Reason} | Rejected: {note.Trim()}";
            if (Reason.Length > 1000) Reason = Reason[..1000];
        }
    }
}

