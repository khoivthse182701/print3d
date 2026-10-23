using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;

namespace TheBoys.Infrastructure.Persistence.Configurations;

public sealed class PaymentAttemptConfiguration : IEntityTypeConfiguration<PaymentAttempt>
{
    public void Configure(EntityTypeBuilder<PaymentAttempt> builder)
    {
        builder.ToTable("PaymentAttempts", t =>
        {
            t.HasCheckConstraint("CK_PaymentAttempts_AmountVnd", "[AmountVnd] > 0");
            t.HasCheckConstraint("CK_PaymentAttempts_Currency", "[Currency] = 'VND'");
            t.HasCheckConstraint("CK_PaymentAttempts_GatewayFee", "[GatewayFeeActualVnd] IS NULL OR [GatewayFeeActualVnd] >= 0");
            t.HasCheckConstraint("CK_PaymentAttempts_Status", "[Status] IN ('Pending','Succeeded','Failed','Expired','NeedsAttention')");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.OrderId)
            .IsRequired();

        builder.Property(x => x.Gateway)
            .HasMaxLength(30)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.MerchantReference)
            .HasMaxLength(150)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.ProviderTransactionId)
            .HasMaxLength(150)
            .IsUnicode(false);

        builder.Property(x => x.Status)
            .HasMaxLength(30)
            .IsUnicode(false)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(x => x.AmountVnd)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.Currency)
            .HasMaxLength(3)
            .IsUnicode(false)
            .HasDefaultValue("VND")
            .IsRequired();

        builder.Property(x => x.GatewayFeeActualVnd)
            .HasPrecision(18, 2);

        builder.Property(x => x.ExpiresAtUtc)
            .HasPrecision(3)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasPrecision(3)
            .IsRequired();

        builder.Property(x => x.CapturedAtUtc)
            .HasPrecision(3);

        builder.Property(x => x.CheckoutUrl)
            .HasMaxLength(1000);

        builder.Property(x => x.FailureReason)
            .HasMaxLength(500);

        builder.Property(x => x.ClientRequestId)
            .HasMaxLength(150)
            .IsUnicode(false);

        builder.Property(x => x.RequestFingerprint)
            .HasMaxLength(150)
            .IsUnicode(false);

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        builder.HasOne(x => x.Order)
            .WithMany(o => o.PaymentAttempts)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.Gateway, x.MerchantReference })
            .IsUnique()
            .HasDatabaseName("UX_PaymentAttempts_Gateway_MerchantReference");

        builder.HasIndex(x => new { x.Gateway, x.ProviderTransactionId })
            .IsUnique()
            .HasFilter("[ProviderTransactionId] IS NOT NULL")
            .HasDatabaseName("UX_PaymentAttempts_Gateway_ProviderTransaction");

        builder.HasIndex(x => new { x.OrderId, x.CreatedAtUtc })
            .HasDatabaseName("IX_PaymentAttempts_OrderId_CreatedAtUtc");

        builder.HasIndex(x => new { x.Status, x.ExpiresAtUtc })
            .HasDatabaseName("IX_PaymentAttempts_Status_ExpiresAtUtc");
    }
}

