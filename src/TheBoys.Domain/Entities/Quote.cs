namespace TheBoys.Domain.Entities;

public sealed class Quote
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public Guid VersionId { get; private set; }
    public byte[] ConfigurationHash { get; private set; } = [];
    public Guid ApprovalId { get; private set; }
    public Guid ReviewId { get; private set; }
    public Guid PricingPackageId { get; private set; }
    public int Quantity { get; private set; }
    public decimal UnitPriceVnd { get; private set; }
    public decimal ShippingFeeVnd { get; private set; }
    public decimal TotalVnd { get; private set; }
    public string AddressJson { get; private set; } = default!;
    public string SnapshotJson { get; private set; } = default!;
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    // Navigation
    public User User { get; private set; } = default!;
    public ModelVersion Version { get; private set; } = default!;
    public CustomerApproval Approval { get; private set; } = default!;
    public PrintReview Review { get; private set; } = default!;
    public PricingPackage PricingPackage { get; private set; } = default!;
    public Order? Order { get; private set; }

    private Quote() { }

    public static Quote Create(
        Guid userId,
        Guid versionId,
        byte[] configurationHash,
        Guid approvalId,
        Guid reviewId,
        Guid pricingPackageId,
        int quantity,
        decimal unitPriceVnd,
        decimal shippingFeeVnd,
        string addressJson,
        string snapshotJson,
        DateTime createdAtUtc,
        DateTime expiresAtUtc)
    {
        if (userId == Guid.Empty) throw new ArgumentException("UserId không được rỗng.", nameof(userId));
        if (versionId == Guid.Empty) throw new ArgumentException("VersionId không được rỗng.", nameof(versionId));
        if (configurationHash == null || configurationHash.Length != 32)
            throw new ArgumentException("ConfigurationHash phải là SHA-256 32 bytes.", nameof(configurationHash));
        if (approvalId == Guid.Empty) throw new ArgumentException("ApprovalId không được rỗng.", nameof(approvalId));
        if (reviewId == Guid.Empty) throw new ArgumentException("ReviewId không được rỗng.", nameof(reviewId));
        if (pricingPackageId == Guid.Empty) throw new ArgumentException("PricingPackageId không được rỗng.", nameof(pricingPackageId));
        if (quantity < 1 || quantity > 10) throw new ArgumentException("Quantity phải từ 1 đến 10.", nameof(quantity));
        if (unitPriceVnd <= 0) throw new ArgumentException("UnitPriceVnd phải lớn hơn 0.", nameof(unitPriceVnd));
        if (shippingFeeVnd < 0) throw new ArgumentException("ShippingFeeVnd không được âm.", nameof(shippingFeeVnd));
        if (string.IsNullOrWhiteSpace(addressJson)) throw new ArgumentException("AddressJson không được rỗng.", nameof(addressJson));
        if (string.IsNullOrWhiteSpace(snapshotJson)) throw new ArgumentException("SnapshotJson không được rỗng.", nameof(snapshotJson));
        if (expiresAtUtc <= createdAtUtc) throw new ArgumentException("ExpiresAtUtc phải lớn hơn CreatedAtUtc.", nameof(expiresAtUtc));

        var subtotal = decimal.Round(unitPriceVnd * quantity, 0, MidpointRounding.AwayFromZero);
        var total = subtotal + shippingFeeVnd;

        return new Quote
        {
            UserId = userId,
            VersionId = versionId,
            ConfigurationHash = configurationHash,
            ApprovalId = approvalId,
            ReviewId = reviewId,
            PricingPackageId = pricingPackageId,
            Quantity = quantity,
            UnitPriceVnd = unitPriceVnd,
            ShippingFeeVnd = shippingFeeVnd,
            TotalVnd = total,
            AddressJson = addressJson,
            SnapshotJson = snapshotJson,
            CreatedAtUtc = createdAtUtc,
            ExpiresAtUtc = expiresAtUtc
        };
    }

    public bool IsExpired(DateTime utcNow) => utcNow > ExpiresAtUtc;
}

