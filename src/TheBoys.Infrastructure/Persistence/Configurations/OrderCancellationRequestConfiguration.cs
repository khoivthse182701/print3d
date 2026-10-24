using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;

namespace TheBoys.Infrastructure.Persistence.Configurations;

public sealed class OrderCancellationRequestConfiguration : IEntityTypeConfiguration<OrderCancellationRequest>
{
    public void Configure(EntityTypeBuilder<OrderCancellationRequest> builder)
    {
        builder.ToTable("OrderCancellationRequests", t =>
        {
            t.HasCheckConstraint("CK_Cancellation_Status", "[Status] IN ('Pending','Approved','Rejected','Completed')");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.OrderId)
            .IsRequired();

        builder.Property(x => x.RequestedByUserId)
            .IsRequired();

        builder.Property(x => x.Reason)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasMaxLength(20)
            .IsUnicode(false)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(x => x.DecidedByUserId);

        builder.Property(x => x.DecisionNote)
            .HasMaxLength(1000);

        builder.Property(x => x.RefundId);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.DecidedAtUtc);

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        builder.HasOne(x => x.Order)
            .WithMany(o => o.CancellationRequests)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RequestedByUser)
            .WithMany()
            .HasForeignKey(x => x.RequestedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.DecidedByUser)
            .WithMany()
            .HasForeignKey(x => x.DecidedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Refund)
            .WithMany(r => r.CancellationRequests)
            .HasForeignKey(x => x.RefundId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.OrderId)
            .IsUnique()
            .HasFilter("[Status] = 'Pending'")
            .HasDatabaseName("UX_Cancellation_Pending");

        builder.HasIndex(x => x.RequestedByUserId)
            .HasDatabaseName("IX_Cancellation_RequestedByUserId");
    }
}

