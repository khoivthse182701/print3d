namespace TheBoys.Domain.Entities;

public sealed class OrderStatusHistory
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid OrderId { get; private set; }
    public string Axis { get; private set; } = "Fulfillment"; // "Fulfillment", "Payment"
    public string? FromStatus { get; private set; }
    public string ToStatus { get; private set; } = default!;
    public Guid? ActorUserId { get; private set; }
    public string Source { get; private set; } = "System";
    public string Reason { get; private set; } = default!;
    public string OperationKey { get; private set; } = default!;
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    // Navigation
    public Order Order { get; private set; } = default!;
    public User? ActorUser { get; private set; }

    private OrderStatusHistory() { }

    public static OrderStatusHistory Create(
        Guid orderId,
        string axis,
        string? fromStatus,
        string toStatus,
        Guid? actorUserId,
        string source,
        string reason,
        string operationKey,
        DateTime createdAtUtc)
    {
        if (orderId == Guid.Empty) throw new ArgumentException("OrderId không được rỗng.", nameof(orderId));
        if (string.IsNullOrWhiteSpace(axis) || (axis != "Fulfillment" && axis != "Payment"))
            throw new ArgumentException("Axis phải là 'Fulfillment' hoặc 'Payment'.", nameof(axis));
        if (string.IsNullOrWhiteSpace(toStatus)) throw new ArgumentException("ToStatus không được rỗng.", nameof(toStatus));
        if (string.IsNullOrWhiteSpace(source)) throw new ArgumentException("Source không được rỗng.", nameof(source));
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Reason không được rỗng.", nameof(reason));
        if (string.IsNullOrWhiteSpace(operationKey)) throw new ArgumentException("OperationKey không được rỗng.", nameof(operationKey));

        return new OrderStatusHistory
        {
            OrderId = orderId,
            Axis = axis,
            FromStatus = fromStatus,
            ToStatus = toStatus.Trim(),
            ActorUserId = actorUserId,
            Source = source.Trim(),
            Reason = reason.Trim(),
            OperationKey = operationKey.Trim(),
            CreatedAtUtc = createdAtUtc
        };
    }
}

