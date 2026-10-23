using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TheBoys.Domain.Entities;

namespace TheBoys.Infrastructure.Persistence.Configurations;

public sealed class OrderStatusHistoryConfiguration : IEntityTypeConfiguration<OrderStatusHistory>
{
    public void Configure(EntityTypeBuilder<OrderStatusHistory> builder)
    {
        builder.ToTable("OrderStatusHistories", t =>
        {
            t.HasCheckConstraint("CK_OrderStatusHistories_Axis", "[Axis] IN ('Fulfillment', 'Payment')");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.OrderId).IsRequired();

        builder.Property(x => x.Axis)
            .HasMaxLength(20)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.FromStatus)
            .HasMaxLength(30)
            .IsUnicode(false);

        builder.Property(x => x.ToStatus)
            .HasMaxLength(30)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.Source)
            .HasMaxLength(30)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.Reason)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.OperationKey)
            .HasMaxLength(150)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasPrecision(3)
            .IsRequired();

        builder.HasOne(x => x.Order)
            .WithMany(o => o.StatusHistories)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ActorUser)
            .WithMany()
            .HasForeignKey(x => x.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.OrderId, x.OperationKey })
            .HasDatabaseName("UX_OrderStatusHistories_OrderId_OperationKey")
            .IsUnique();

        builder.HasIndex(x => new { x.OrderId, x.CreatedAtUtc })
            .HasDatabaseName("IX_OrderStatusHistories_OrderId_CreatedAtUtc");
    }
}

