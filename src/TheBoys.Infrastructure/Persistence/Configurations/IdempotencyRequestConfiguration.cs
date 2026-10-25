using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TheBoys.Domain.Entities;

namespace TheBoys.Infrastructure.Persistence.Configurations;

public sealed class IdempotencyRequestConfiguration : IEntityTypeConfiguration<IdempotencyRequest>
{
    public void Configure(EntityTypeBuilder<IdempotencyRequest> builder)
    {
        builder.ToTable("IdempotencyRequests", t =>
        {
            t.HasCheckConstraint("CK_IdempotencyRequests_ResponseStatus", "[ResponseStatus] IS NULL OR ([ResponseStatus] >= 200 AND [ResponseStatus] <= 599)");
            t.HasCheckConstraint("CK_IdempotencyRequests_CompletedState", "[Status] <> 'Completed' OR ([ResponseStatus] IS NOT NULL AND [CompletedAtUtc] IS NOT NULL)");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Operation)
            .HasMaxLength(100)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.ClientRequestId)
            .IsRequired();

        builder.Property(x => x.RequestFingerprint)
            .HasMaxLength(32)
            .IsFixedLength()
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.ResourceType)
            .HasMaxLength(50)
            .IsUnicode(false);

        builder.Property(x => x.CreatedAtUtc)
            .HasPrecision(3)
            .IsRequired();

        builder.Property(x => x.CompletedAtUtc)
            .HasPrecision(3);

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.UserId, x.Operation, x.ClientRequestId })
            .HasDatabaseName("UX_IdempotencyRequests_UserOpClient")
            .IsUnique();
    }
}

