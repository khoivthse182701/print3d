using TheBoys.Domain.Common;

namespace TheBoys.Domain.Entities;

public sealed class ShipmentEvent : BaseEntity
{
    public Guid ShipmentId { get; private set; }
    public string EventKey { get; private set; } = default!;
    public string Status { get; private set; } = default!;
    public string? Note { get; private set; }
    public string Source { get; private set; } = default!;
    public Guid? ActorUserId { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public DateTime ReceivedAtUtc { get; private set; } = DateTime.UtcNow;

    // Navigation
    public Shipment Shipment { get; private set; } = default!;
    public User? ActorUser { get; private set; }

    private ShipmentEvent() { }

    public static ShipmentEvent Create(
        Guid shipmentId,
        string eventKey,
        string status,
        string? note,
        string source,
        Guid? actorUserId,
        DateTime occurredAtUtc,
        DateTime receivedAtUtc)
    {
        if (shipmentId == Guid.Empty) throw new ArgumentException("ShipmentId không được rỗng.", nameof(shipmentId));
        if (string.IsNullOrWhiteSpace(eventKey)) throw new ArgumentException("EventKey không được rỗng.", nameof(eventKey));
        if (string.IsNullOrWhiteSpace(status)) throw new ArgumentException("Status không được rỗng.", nameof(status));
        if (string.IsNullOrWhiteSpace(source)) throw new ArgumentException("Source không được rỗng.", nameof(source));

        return new ShipmentEvent
        {
            ShipmentId = shipmentId,
            EventKey = eventKey.Trim(),
            Status = status.Trim(),
            Note = note?.Trim(),
            Source = source.Trim(),
            ActorUserId = actorUserId,
            OccurredAtUtc = occurredAtUtc,
            ReceivedAtUtc = receivedAtUtc
        };
    }
}

