namespace TheBoys.Domain.Entities;

public sealed class OrderItem
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid OrderId { get; private set; }
    public int LineNumber { get; private set; }
    public Guid VersionId { get; private set; }
    public byte[] ConfigurationHash { get; private set; } = [];
    public byte[] AssetHash { get; private set; } = [];
    public int Quantity { get; private set; }
    public decimal UnitPriceVnd { get; private set; }
    public string MaterialSnapshot { get; private set; } = default!;
    public decimal HeightMm { get; private set; }
    public string? EngravingText { get; private set; }
    public decimal PartnerUnitBudgetVnd { get; private set; }

    // Navigation
    public Order Order { get; private set; } = default!;
    public ModelVersion Version { get; private set; } = default!;

    private OrderItem() { }

    public static OrderItem Create(
        Guid orderId,
        int lineNumber,
        Guid versionId,
        byte[] configurationHash,
        byte[] assetHash,
        int quantity,
        decimal unitPriceVnd,
        string materialSnapshot,
        decimal heightMm,
        string? engravingText,
        decimal partnerUnitBudgetVnd)
    {
        if (orderId == Guid.Empty) throw new ArgumentException("OrderId không được rỗng.", nameof(orderId));
        if (lineNumber <= 0) throw new ArgumentException("LineNumber phải lớn hơn 0.", nameof(lineNumber));
        if (versionId == Guid.Empty) throw new ArgumentException("VersionId không được rỗng.", nameof(versionId));
        if (configurationHash == null || configurationHash.Length != 32)
            throw new ArgumentException("ConfigurationHash phải là SHA-256 32 bytes.", nameof(configurationHash));
        if (quantity < 1 || quantity > 10) throw new ArgumentException("Quantity phải từ 1 đến 10.", nameof(quantity));
        if (unitPriceVnd <= 0) throw new ArgumentException("UnitPriceVnd phải lớn hơn 0.", nameof(unitPriceVnd));
        if (string.IsNullOrWhiteSpace(materialSnapshot)) throw new ArgumentException("MaterialSnapshot không được rỗng.", nameof(materialSnapshot));
        if (heightMm <= 0) throw new ArgumentException("HeightMm phải lớn hơn 0.", nameof(heightMm));
        if (partnerUnitBudgetVnd < 0) throw new ArgumentException("PartnerUnitBudgetVnd không được âm.", nameof(partnerUnitBudgetVnd));

        return new OrderItem
        {
            OrderId = orderId,
            LineNumber = lineNumber,
            VersionId = versionId,
            ConfigurationHash = configurationHash,
            AssetHash = assetHash ?? [],
            Quantity = quantity,
            UnitPriceVnd = unitPriceVnd,
            MaterialSnapshot = materialSnapshot.Trim(),
            HeightMm = heightMm,
            EngravingText = string.IsNullOrWhiteSpace(engravingText) ? null : engravingText.Trim(),
            PartnerUnitBudgetVnd = partnerUnitBudgetVnd
        };
    }
}

