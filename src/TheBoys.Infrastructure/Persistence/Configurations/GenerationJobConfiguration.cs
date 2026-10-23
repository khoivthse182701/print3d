using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TheBoys.Domain.Entities;

namespace TheBoys.Infrastructure.Persistence.Configurations;

public sealed class GenerationJobConfiguration : IEntityTypeConfiguration<GenerationJob>
{
    public void Configure(EntityTypeBuilder<GenerationJob> builder)
    {
        builder.ToTable("GenerationJobs", t =>
        {
            t.HasCheckConstraint("CK_GenerationJobs_Operation", "[Operation] IN ('ImageTo3D','TextTo3D','Resize','Repair')");
            t.HasCheckConstraint("CK_GenerationJobs_Status", "[Status] IN ('Queued','Processing','Succeeded','Failed','NeedsAttention','Cancelled')");
            t.HasCheckConstraint("CK_GenerationJobs_RequestedHeight", "[RequestedHeightMm] > 0");
            t.HasCheckConstraint("CK_GenerationJobs_Completion", "[CompletedAtUtc] IS NULL OR [CompletedAtUtc] >= [CreatedAtUtc]");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Operation)
            .HasMaxLength(30)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.Provider)
            .HasMaxLength(30)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.ProviderModel)
            .HasMaxLength(100)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.ApiVersion)
            .HasMaxLength(40)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.PolicyVersion)
            .HasMaxLength(40)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.PricingVersion)
            .HasMaxLength(40)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.AccountScope)
            .HasMaxLength(100)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.Prompt)
            .HasMaxLength(4000);

        builder.Property(x => x.InputStorageKey)
            .HasMaxLength(500);

        builder.Property(x => x.InputHash)
            .HasMaxLength(32)
            .IsFixedLength()
            .IsRequired();

        builder.Property(x => x.RequestedHeightMm)
            .HasPrecision(10, 3)
            .IsRequired();

        builder.Property(x => x.ShouldTexture)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasMaxLength(30)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.RequestFingerprint)
            .HasMaxLength(32)
            .IsFixedLength()
            .IsRequired();

        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(2000);

        builder.Property(x => x.CreatedAtUtc)
            .HasPrecision(3)
            .IsRequired();

        builder.Property(x => x.CompletedAtUtc)
            .HasPrecision(3);

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        builder.HasIndex(x => new { x.UserId, x.ClientRequestId })
            .HasDatabaseName("UX_GenerationJobs_User_ClientRequestId")
            .IsUnique();

        builder.HasIndex(x => x.ProjectId)
            .HasDatabaseName("IX_GenerationJobs_ProjectId");

        builder.HasIndex(x => x.UserId)
            .HasDatabaseName("IX_GenerationJobs_UserId");

        builder.HasIndex(x => new { x.Status, x.CreatedAtUtc })
            .HasDatabaseName("IX_GenerationJobs_Status_CreatedAtUtc");

        builder.HasOne(x => x.Project)
            .WithMany(p => p.Jobs)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.InputConsent)
            .WithMany()
            .HasForeignKey(x => x.InputConsentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.SourceVersion)
            .WithMany()
            .HasForeignKey(x => x.SourceVersionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

