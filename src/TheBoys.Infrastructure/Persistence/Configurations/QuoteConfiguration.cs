using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TheBoys.Domain.Entities;

namespace TheBoys.Infrastructure.Persistence.Configurations;

public sealed class QuoteConfiguration : IEntityTypeConfiguration<Quote>
{
    public void Configure(EntityTypeBuilder<Quote> builder)
    {
        builder.ToTable("Quotes", t =>
        {
            t.HasCheckConstraint("CK_Quotes_Quantity", "[Quantity] >= 1 AND [Quantity] <= 10");
            t.HasCheckConstraint("CK_Quotes_Financials", "[UnitPriceVnd] > 0 AND [ShippingFeeVnd] >= 0 AND [TotalVnd] = [UnitPriceVnd] * [Quantity] + [ShippingFeeVnd]");
            t.HasCheckConstraint("CK_Quotes_Expiry", "[ExpiresAtUtc] > [CreatedAtUtc]");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId).IsRequired();
        builder.Property(x => x.VersionId).IsRequired();
        builder.Property(x => x.ApprovalId).IsRequired();
        builder.Property(x => x.ReviewId).IsRequired();
        builder.Property(x => x.PricingPackageId).IsRequired();

        builder.Property(x => x.ConfigurationHash)
            .HasMaxLength(32)
            .IsFixedLength()
            .IsRequired();

        builder.Property(x => x.Quantity)
            .IsRequired();

        builder.Property(x => x.UnitPriceVnd)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.ShippingFeeVnd)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.TotalVnd)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.AddressJson)
            .IsRequired();

        builder.Property(x => x.SnapshotJson)
            .IsRequired();

        builder.Property(x => x.ExpiresAtUtc)
            .HasPrecision(3)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasPrecision(3)
            .IsRequired();

        builder.HasOne(x => x.User)
            .WithMany(u => u.Quotes)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Version)
            .WithMany(v => v.Quotes)
            .HasForeignKey(x => x.VersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Approval)
            .WithMany()
            .HasForeignKey(x => x.ApprovalId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Review)
            .WithMany()
            .HasForeignKey(x => x.ReviewId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.PricingPackage)
            .WithMany()
            .HasForeignKey(x => x.PricingPackageId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.UserId, x.CreatedAtUtc })
            .HasDatabaseName("IX_Quotes_UserId_CreatedAtUtc");

        builder.HasIndex(x => new { x.VersionId, x.ConfigurationHash })
            .HasDatabaseName("IX_Quotes_Version_ConfigHash");

        builder.HasIndex(x => x.ExpiresAtUtc)
            .HasDatabaseName("IX_Quotes_ExpiresAtUtc");
    }
}

