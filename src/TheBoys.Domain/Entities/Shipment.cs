using TheBoys.Domain.Common;
using TheBoys.Domain.Enums;

namespace TheBoys.Domain.Entities;

public sealed class Shipment : BaseEntity
{
    public Guid OrderId { get; private set; }
    public string Carrier { get; private set; } = default!;
    public string TrackingNumber { get; private set; } = default!;
    public ShipmentStatus Status { get; private set; } = ShipmentStatus.Created;
    public decimal? ShippingActualVnd { get; private set; }
    public DateTime? DeliveredAtUtc { get; private set; }
    public string? DeliveryEvidenceStorageKey { get; private set; }
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    // Navigation
    public Order Order { get; private set; } = default!;
    public ICollection<ShipmentEvent> Events { get; private set; } = new List<ShipmentEvent>();

    private Shipment() { }

    public static Shipment Create(
        Guid orderId,
        string carrier,
        string trackingNumber,
        decimal? shippingActualVnd,
        DateTime createdAtUtc)
    {
        if (orderId == Guid.Empty) throw new ArgumentException("OrderId không được rỗng.", nameof(orderId));
        if (string.IsNullOrWhiteSpace(carrier)) throw new ArgumentException("Carrier không được rỗng.", nameof(carrier));
        if (string.IsNullOrWhiteSpace(trackingNumber)) throw new ArgumentException("TrackingNumber không được rỗng.", nameof(trackingNumber));
        if (shippingActualVnd.HasValue && shippingActualVnd.Value < 0)
            throw new ArgumentException("ShippingActualVnd phải >= 0 nếu có giá trị.", nameof(shippingActualVnd));

        return new Shipment
        {
            OrderId = orderId,
            Carrier = carrier.Trim(),
            TrackingNumber = trackingNumber.Trim(),
            Status = ShipmentStatus.Created,
            ShippingActualVnd = shippingActualVnd,
            CreatedAtUtc = createdAtUtc
        };
    }

    public void MarkShipping()
    {
        Status = ShipmentStatus.Shipping;
    }

    public void MarkDelivered(DateTime deliveredAtUtc, string deliveryEvidenceStorageKey)
    {
        if (string.IsNullOrWhiteSpace(deliveryEvidenceStorageKey))
            throw new ArgumentException("DeliveryEvidenceStorageKey không được rỗng khi giao hàng thành công.", nameof(deliveryEvidenceStorageKey));

        Status = ShipmentStatus.Delivered;
        DeliveredAtUtc = deliveredAtUtc;
        DeliveryEvidenceStorageKey = deliveryEvidenceStorageKey.Trim();
    }

    public void MarkDeliveryFailed()
    {
        Status = ShipmentStatus.DeliveryFailed;
    }

    public void MarkReturned()
    {
        Status = ShipmentStatus.Returned;
    }

    public void Cancel()
    {
        Status = ShipmentStatus.Cancelled;
    }
}

