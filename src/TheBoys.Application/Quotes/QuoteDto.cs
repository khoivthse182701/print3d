namespace TheBoys.Application.Quotes;

public sealed record QuoteDto(
    Guid Id,
    Guid UserId,
    Guid VersionId,
    string ConfigurationHash,
    Guid ApprovalId,
    Guid ReviewId,
    Guid PricingPackageId,
    string PackageCode,
    string PackageName,
    int Quantity,
    decimal UnitPriceVnd,
    decimal ShippingFeeVnd,
    decimal TotalVnd,
    ShippingAddressDto Address,
    string SnapshotJson,
    DateTime ExpiresAtUtc,
    DateTime CreatedAtUtc,
    bool IsExpired
);

