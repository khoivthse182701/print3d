using TheBoys.Domain.Common;
using TheBoys.Domain.Enums;

namespace TheBoys.Domain.Entities;

public sealed class IdempotencyRequest : BaseEntity
{
    public Guid UserId { get; private set; }
    public string Operation { get; private set; } = default!;
    public Guid ClientRequestId { get; private set; }
    public byte[] RequestFingerprint { get; private set; } = [];
    public IdempotencyStatus Status { get; private set; } = IdempotencyStatus.Processing;
    public string? ResourceType { get; private set; }
    public Guid? ResourceId { get; private set; }
    public int? ResponseStatus { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    // Navigation
    public User? User { get; private set; }

    private IdempotencyRequest() { }

    public static IdempotencyRequest Start(
        Guid userId,
        string operation,
        Guid clientRequestId,
        byte[] requestFingerprint,
        DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(operation)) throw new ArgumentException("Operation không được rỗng.", nameof(operation));
        if (requestFingerprint == null || requestFingerprint.Length == 0) throw new ArgumentException("Fingerprint không được rỗng.", nameof(requestFingerprint));

        return new IdempotencyRequest
        {
            UserId = userId,
            Operation = operation.Trim(),
            ClientRequestId = clientRequestId,
            RequestFingerprint = requestFingerprint,
            Status = IdempotencyStatus.Processing,
            CreatedAtUtc = utcNow
        };
    }

    public void Complete(int responseStatus, string? resourceType, Guid? resourceId, DateTime utcNow)
    {
        Status = IdempotencyStatus.Completed;
        ResponseStatus = responseStatus;
        ResourceType = resourceType;
        ResourceId = resourceId;
        CompletedAtUtc = utcNow;
    }

    public void MarkNeedsAttention()
    {
        Status = IdempotencyStatus.NeedsAttention;
    }
}

