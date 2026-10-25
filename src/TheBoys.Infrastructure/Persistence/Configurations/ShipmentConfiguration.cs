using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TheBoys.Domain.Entities;

namespace TheBoys.Infrastructure.Persistence.Configurations;

public sealed class ShipmentConfiguration : IEntityTypeConfiguration<Shipment>
{
    public void Configure(EntityTypeBuilder<Shipment> builder)
    {
        builder.ToTable("Shipments", t =>
        {
            t.HasCheckConstraint("CK_Shipments_Status", "[Status] IN ('Created','Shipping','DeliveryFailed','Delivered','Returned','Cancelled')");
            t.HasCheckConstraint("CK_Shipments_DeliveredEvidence", "([Status] <> 'Delivered' OR ([DeliveredAtUtc] IS NOT NULL AND [DeliveryEvidenceStorageKey] IS NOT NULL))");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Carrier)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.TrackingNumber)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(30)
            .HasConversion<string>();

        builder.Property(x => x.ShippingActualVnd)
            .HasPrecision(18, 2);

        builder.Property(x => x.DeliveryEvidenceStorageKey)
            .HasMaxLength(500);

        builder.HasOne(x => x.Order)
            .WithMany(o => o.Shipments)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.Carrier, x.TrackingNumber })
            .IsUnique();
    }
}

