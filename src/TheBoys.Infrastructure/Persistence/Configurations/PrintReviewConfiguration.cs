using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TheBoys.Domain.Entities;

namespace TheBoys.Infrastructure.Persistence.Configurations;

public sealed class PrintReviewConfiguration : IEntityTypeConfiguration<PrintReview>
{
    public void Configure(EntityTypeBuilder<PrintReview> builder)
    {
        builder.ToTable("PrintReviews", t =>
        {
            t.HasCheckConstraint("CK_PrintReviews_Decision", "[Decision] IN ('Approved', 'Rejected')");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.VersionId)
            .IsRequired();

        builder.Property(x => x.ConfigurationHash)
            .HasMaxLength(32)
            .IsFixedLength()
            .IsRequired();

        builder.Property(x => x.ReviewerUserId)
            .IsRequired();

        builder.Property(x => x.Decision)
            .HasMaxLength(20)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.ChecklistJson)
            .IsRequired();

        builder.Property(x => x.EvidenceStorageKey)
            .HasMaxLength(500);

        builder.Property(x => x.Note)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasPrecision(3)
            .IsRequired();

        builder.HasOne(x => x.Version)
            .WithMany(v => v.Reviews)
            .HasForeignKey(x => x.VersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ReviewerUser)
            .WithMany(u => u.PrintReviews)
            .HasForeignKey(x => x.ReviewerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.VersionId, x.ConfigurationHash, x.CreatedAtUtc })
            .HasDatabaseName("IX_PrintReviews_Version_ConfigHash");

        builder.HasIndex(x => x.ReviewerUserId)
            .HasDatabaseName("IX_PrintReviews_ReviewerUserId");
    }
}

