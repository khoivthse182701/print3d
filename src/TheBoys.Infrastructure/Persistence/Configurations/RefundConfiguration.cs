using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;

namespace TheBoys.Infrastructure.Persistence.Configurations;

public sealed class RefundConfiguration : IEntityTypeConfiguration<Refund>
{
    public void Configure(EntityTypeBuilder<Refund> builder)
    {
        builder.ToTable("Refunds", t =>
        {
            t.HasCheckConstraint("CK_Refunds_AmountVnd", "[AmountVnd] > 0");
            t.HasCheckConstraint("CK_Refunds_Status", "[Status] IN ('Requested','Reserved','Submitted','Succeeded','Failed','NeedsAttention','Rejected')");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.PaymentAttemptId)
            .IsRequired();

        builder.Property(x => x.RequestedByUserId)
            .IsRequired();

        builder.Property(x => x.ApprovedByUserId);

        builder.Property(x => x.OperationKey)
            .IsRequired();

        builder.Property(x => x.ProviderReference)
            .HasMaxLength(150)
            .IsUnicode(false);

        builder.Property(x => x.AmountVnd)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasMaxLength(30)
            .IsUnicode(false)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(x => x.Reason)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.CompletedAtUtc);

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        builder.HasOne(x => x.PaymentAttempt)
            .WithMany(p => p.Refunds)
            .HasForeignKey(x => x.PaymentAttemptId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RequestedByUser)
            .WithMany()
            .HasForeignKey(x => x.RequestedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ApprovedByUser)
            .WithMany()
            .HasForeignKey(x => x.ApprovedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.OperationKey)
            .IsUnique()
            .HasDatabaseName("UX_Refunds_OperationKey");

        builder.HasIndex(x => new { x.PaymentAttemptId, x.Status })
            .IncludeProperties(x => x.AmountVnd)
            .HasDatabaseName("IX_Refunds_PaymentStatus");

        builder.HasIndex(x => x.RequestedByUserId)
            .HasDatabaseName("IX_Refunds_RequestedByUserId");
    }
}

