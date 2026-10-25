using TheBoys.Domain.Common;
using TheBoys.Domain.Enums;

namespace TheBoys.Domain.Entities;

public sealed class OrderCancellationRequest : BaseEntity
{
    public Guid OrderId { get; private set; }
    public Guid RequestedByUserId { get; private set; }
    public string Reason { get; private set; } = default!;
    public CancellationRequestStatus Status { get; private set; } = CancellationRequestStatus.Pending;
    public Guid? DecidedByUserId { get; private set; }
    public string? DecisionNote { get; private set; }
    public Guid? RefundId { get; set; }
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? DecidedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    // Navigation
    public Order Order { get; private set; } = default!;
    public User RequestedByUser { get; private set; } = default!;
    public User? DecidedByUser { get; private set; }
    public Refund? Refund { get; private set; }

    private OrderCancellationRequest() { }

    public static OrderCancellationRequest Create(
        Guid orderId,
        Guid requestedByUserId,
        string reason,
        DateTime createdAtUtc)
    {
        if (orderId == Guid.Empty) throw new ArgumentException("OrderId không được rỗng.", nameof(orderId));
        if (requestedByUserId == Guid.Empty) throw new ArgumentException("RequestedByUserId không được rỗng.", nameof(requestedByUserId));
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Reason không được rỗng.", nameof(reason));

        return new OrderCancellationRequest
        {
            OrderId = orderId,
            RequestedByUserId = requestedByUserId,
            Reason = reason.Trim(),
            Status = CancellationRequestStatus.Pending,
            CreatedAtUtc = createdAtUtc
        };
    }

    public void Approve(Guid decidedByUserId, string? decisionNote, Guid? refundId, DateTime decidedAtUtc)
    {
        Status = CancellationRequestStatus.Approved;
        DecidedByUserId = decidedByUserId;
        DecisionNote = decisionNote?.Trim();
        if (refundId.HasValue && refundId.Value != Guid.Empty)
        {
            RefundId = refundId.Value;
        }
        DecidedAtUtc = decidedAtUtc;
    }

    public void Reject(Guid decidedByUserId, string? decisionNote, DateTime decidedAtUtc)
    {
        Status = CancellationRequestStatus.Rejected;
        DecidedByUserId = decidedByUserId;
        DecisionNote = decisionNote?.Trim();
        DecidedAtUtc = decidedAtUtc;
    }

    public void Complete(DateTime completedAtUtc)
    {
        Status = CancellationRequestStatus.Completed;
        DecidedAtUtc ??= completedAtUtc;
    }
}

