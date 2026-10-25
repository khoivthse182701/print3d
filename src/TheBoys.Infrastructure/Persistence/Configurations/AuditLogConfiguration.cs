using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TheBoys.Domain.Entities;

namespace TheBoys.Infrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs", t =>
        {
            t.HasCheckConstraint("CK_AuditLogs_JsonValid", "ISJSON([RedactedChangesJson]) = 1");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Action)
            .HasMaxLength(100)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.AggregateType)
            .HasMaxLength(50)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.AggregateId)
            .IsRequired();

        builder.Property(x => x.TraceId)
            .HasMaxLength(64)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.RedactedChangesJson)
            .IsRequired();

        builder.Property(x => x.Reason)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasPrecision(3)
            .IsRequired();

        builder.HasOne(x => x.ActorUser)
            .WithMany()
            .HasForeignKey(x => x.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.AggregateType, x.AggregateId, x.CreatedAtUtc })
            .HasDatabaseName("IX_Audit_AggregateTime");
    }
}

