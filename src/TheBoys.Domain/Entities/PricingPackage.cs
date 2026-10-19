using TheBoys.Domain.Common;

namespace TheBoys.Domain.Entities;

public sealed class PricingPackage : BaseEntity
{
    public string PackageCode { get; set; } = default!; // 'TIER_STANDARD_390', 'TIER_BASELINE_300'
    public string Name { get; set; } = default!;
    public decimal PriceVnd { get; set; } // 390.000 hoặc 300.000 VNĐ
    public decimal PartnerBaseCostVnd { get; set; } = 120000.00m; // 120.000 VNĐ theo CP3
    public string Material { get; set; } = default!; // 'Resin 16K', 'PLA'
    public string HeightRangeCm { get; set; } = default!; // '10 - 12 cm'
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class PartnerLab : BaseEntity
{
    public Guid? UserId { get; set; }
    public string LabName { get; set; } = default!;
    public string ContactPerson { get; set; } = default!;
    public string PhoneNumber { get; set; } = default!;
    public string? Email { get; set; }
    public string District { get; set; } = default!;
    public string Address { get; set; } = default!;
    public int CapacityPerDay { get; set; } = 20;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation
    public User? User { get; set; }
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
