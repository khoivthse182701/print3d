using TheBoys.Domain.Common;
using TheBoys.Domain.Enums;

namespace TheBoys.Domain.Entities;

public sealed class PaymentEvent : BaseEntity
{
    public Guid PaymentAttemptId { get; private set; }
    public string Gateway { get; private set; } = default!;
    public string EventId { get; private set; } = default!;
    public byte[] PayloadHash { get; private set; } = Array.Empty<byte>();
    public DateTime VerifiedAtUtc { get; private set; }
    public DateTime ReceivedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? ProcessedAtUtc { get; private set; }
    public PaymentEventProcessingResult ProcessingResult { get; private set; } = PaymentEventProcessingResult.Pending;
    public string? Note { get; private set; }

    // Navigation
    public PaymentAttempt PaymentAttempt { get; private set; } = default!;

    private PaymentEvent() { }

    public static PaymentEvent Create(
        Guid paymentAttemptId,
        string gateway,
        string eventId,
        byte[] payloadHash,
        DateTime verifiedAtUtc,
        DateTime receivedAtUtc)
    {
        if (paymentAttemptId == Guid.Empty) throw new ArgumentException("PaymentAttemptId không được rỗng.", nameof(paymentAttemptId));
        if (string.IsNullOrWhiteSpace(gateway)) throw new ArgumentException("Gateway không được rỗng.", nameof(gateway));
        if (string.IsNullOrWhiteSpace(eventId)) throw new ArgumentException("EventId không được rỗng.", nameof(eventId));
        if (payloadHash == null || payloadHash.Length != 32) throw new ArgumentException("PayloadHash phải là SHA-256 32 bytes.", nameof(payloadHash));

        return new PaymentEvent
        {
            PaymentAttemptId = paymentAttemptId,
            Gateway = gateway.Trim(),
            EventId = eventId.Trim(),
            PayloadHash = payloadHash,
            VerifiedAtUtc = verifiedAtUtc,
            ReceivedAtUtc = receivedAtUtc,
            ProcessingResult = PaymentEventProcessingResult.Pending
        };
    }

    public void MarkProcessed(PaymentEventProcessingResult result, DateTime processedAtUtc, string? note = null)
    {
        ProcessingResult = result;
        ProcessedAtUtc = processedAtUtc;
        if (!string.IsNullOrWhiteSpace(note))
        {
            Note = note.Trim();
        }
    }
}

