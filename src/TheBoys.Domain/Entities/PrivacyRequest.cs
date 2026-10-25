using TheBoys.Domain.Common;
using TheBoys.Domain.Enums;

namespace TheBoys.Domain.Entities;

public sealed class PrivacyRequest : BaseEntity
{
    public Guid UserId { get; private set; }
    public PrivacyRequestType RequestType { get; private set; }
    public PrivacyRequestStatus Status { get; private set; } = PrivacyRequestStatus.Pending;
    public string Reason { get; private set; } = default!;
    public string? DecisionNote { get; private set; }
    public Guid? HandledByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    // Navigation
    public User User { get; private set; } = default!;
    public User? HandledByUser { get; private set; }

    private PrivacyRequest() { }

    public static PrivacyRequest Create(
        Guid userId,
        PrivacyRequestType requestType,
        string reason,
        DateTime utcNow)
    {
        if (userId == Guid.Empty) throw new ArgumentException("UserId không được rỗng.", nameof(userId));
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Reason không được rỗng.", nameof(reason));

        return new PrivacyRequest
        {
            UserId = userId,
            RequestType = requestType,
            Status = PrivacyRequestStatus.Pending,
            Reason = reason.Trim(),
            CreatedAtUtc = utcNow
        };
    }

    public void TransitionToProcessing(Guid handledByUserId, string? note = null)
    {
        HandledByUserId = handledByUserId;
        Status = PrivacyRequestStatus.Processing;
        if (!string.IsNullOrWhiteSpace(note))
        {
            DecisionNote = note.Trim();
        }
    }

    public void Reject(Guid handledByUserId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Reason từ chối không được rỗng.", nameof(reason));
        HandledByUserId = handledByUserId;
        Status = PrivacyRequestStatus.Rejected;
        DecisionNote = reason.Trim();
    }

    public void Complete(Guid handledByUserId, DateTime completedAtUtc, string? decisionNote = null)
    {
        HandledByUserId = handledByUserId;
        Status = PrivacyRequestStatus.Completed;
        CompletedAtUtc = completedAtUtc;
        if (!string.IsNullOrWhiteSpace(decisionNote))
        {
            DecisionNote = decisionNote.Trim();
        }
    }
}

