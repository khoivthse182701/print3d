using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TheBoys.Domain.Entities;

namespace TheBoys.Infrastructure.Persistence.Configurations;

public sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("OrderItems", t =>
        {
            t.HasCheckConstraint("CK_OrderItems_LineNumber", "[LineNumber] > 0");
            t.HasCheckConstraint("CK_OrderItems_Quantity", "[Quantity] >= 1 AND [Quantity] <= 10");
            t.HasCheckConstraint("CK_OrderItems_UnitPrice", "[UnitPriceVnd] > 0");
            t.HasCheckConstraint("CK_OrderItems_HeightMm", "[HeightMm] > 0");
            t.HasCheckConstraint("CK_OrderItems_PartnerBudget", "[PartnerUnitBudgetVnd] >= 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.OrderId).IsRequired();
        builder.Property(x => x.LineNumber).IsRequired();
        builder.Property(x => x.VersionId).IsRequired();

        builder.Property(x => x.ConfigurationHash)
            .HasMaxLength(32)
            .IsFixedLength()
            .IsRequired();

        builder.Property(x => x.AssetHash)
            .HasMaxLength(32)
            .IsFixedLength()
            .IsRequired();

        builder.Property(x => x.Quantity)
            .IsRequired();

        builder.Property(x => x.UnitPriceVnd)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.MaterialSnapshot)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.HeightMm)
            .HasPrecision(10, 3)
            .IsRequired();

        builder.Property(x => x.EngravingText)
            .HasMaxLength(150);

        builder.Property(x => x.PartnerUnitBudgetVnd)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.HasOne(x => x.Order)
            .WithMany(o => o.Items)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Version)
            .WithMany(v => v.OrderItems)
            .HasForeignKey(x => x.VersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.OrderId, x.LineNumber })
            .HasDatabaseName("UX_OrderItems_OrderId_LineNumber")
            .IsUnique();

        builder.HasIndex(x => x.VersionId)
            .HasDatabaseName("IX_OrderItems_VersionId");
    }
}

