using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TheBoys.Domain.Entities;

namespace TheBoys.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages", t =>
        {
            t.HasCheckConstraint("CK_OutboxMessages_JsonValid", "ISJSON([PayloadJson]) = 1");
            t.HasCheckConstraint("CK_OutboxMessages_AttemptNonNegative", "[AttemptCount] >= 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.EventType)
            .HasMaxLength(100)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.AggregateId)
            .IsRequired();

        builder.Property(x => x.DeduplicationKey)
            .HasMaxLength(150)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.PayloadJson)
            .IsRequired();

        builder.Property(x => x.AttemptCount)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.AvailableAtUtc)
            .HasPrecision(3)
            .IsRequired();

        builder.Property(x => x.LeaseOwner)
            .HasMaxLength(100)
            .IsUnicode(false);

        builder.Property(x => x.LeaseExpiresAtUtc)
            .HasPrecision(3);

        builder.Property(x => x.ProcessedAtUtc)
            .HasPrecision(3);

        builder.Property(x => x.DeadLetteredAtUtc)
            .HasPrecision(3);

        builder.Property(x => x.CreatedAtUtc)
            .HasPrecision(3)
            .IsRequired();

        builder.HasIndex(x => x.DeduplicationKey)
            .HasDatabaseName("UX_Outbox_DeduplicationKey")
            .IsUnique();

        builder.HasIndex(x => x.AvailableAtUtc)
            .HasDatabaseName("IX_Outbox_Pending")
            .IncludeProperties(x => x.LeaseExpiresAtUtc)
            .HasFilter("[ProcessedAtUtc] IS NULL AND [DeadLetteredAtUtc] IS NULL");
    }
}

