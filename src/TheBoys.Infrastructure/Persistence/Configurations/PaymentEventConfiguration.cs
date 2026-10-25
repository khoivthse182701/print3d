using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;

namespace TheBoys.Infrastructure.Persistence.Configurations;

public sealed class PaymentEventConfiguration : IEntityTypeConfiguration<PaymentEvent>
{
    public void Configure(EntityTypeBuilder<PaymentEvent> builder)
    {
        builder.ToTable("PaymentEvents", t =>
        {
            t.HasCheckConstraint("CK_PaymentEvents_ProcessingResult", "[ProcessingResult] IN ('Pending','Applied','Ignored','NeedsAttention')");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.PaymentAttemptId)
            .IsRequired();

        builder.Property(x => x.Gateway)
            .HasMaxLength(30)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.EventId)
            .HasMaxLength(150)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.PayloadHash)
            .HasMaxLength(32)
            .IsFixedLength()
            .IsRequired();

        builder.Property(x => x.VerifiedAtUtc)
            .HasPrecision(3)
            .IsRequired();

        builder.Property(x => x.ReceivedAtUtc)
            .HasPrecision(3)
            .IsRequired();

        builder.Property(x => x.ProcessedAtUtc)
            .HasPrecision(3);

        builder.Property(x => x.ProcessingResult)
            .HasMaxLength(30)
            .IsUnicode(false)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(x => x.Note)
            .HasMaxLength(500);

        builder.HasOne(x => x.PaymentAttempt)
            .WithMany(pa => pa.Events)
            .HasForeignKey(x => x.PaymentAttemptId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.Gateway, x.EventId })
            .IsUnique()
            .HasDatabaseName("UX_PaymentEvents_Gateway_EventId");

        builder.HasIndex(x => new { x.PaymentAttemptId, x.ReceivedAtUtc })
            .HasDatabaseName("IX_PaymentEvents_PaymentAttempt_ReceivedAtUtc");
    }
}

