using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TheBoys.Domain.Entities;

namespace TheBoys.Infrastructure.Persistence.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications", t =>
        {
            t.HasCheckConstraint("CK_Notifications_Channel", "[Channel] IN ('InApp','Email')");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId)
            .IsRequired();

        builder.Property(x => x.OutboxMessageId)
            .IsRequired();

        builder.Property(x => x.Channel)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Body)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(x => x.DeliveredAtUtc)
            .HasPrecision(3);

        builder.Property(x => x.ReadAtUtc)
            .HasPrecision(3);

        builder.Property(x => x.CreatedAtUtc)
            .HasPrecision(3)
            .IsRequired();

        builder.HasIndex(x => new { x.UserId, x.OutboxMessageId, x.Channel })
            .HasDatabaseName("UX_Notifications_UserOutboxChannel")
            .IsUnique();

        builder.HasOne(x => x.User)
            .WithMany(u => u.Notifications)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.OutboxMessage)
            .WithMany(o => o.Notifications)
            .HasForeignKey(x => x.OutboxMessageId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

