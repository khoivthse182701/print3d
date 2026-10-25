using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TheBoys.Domain.Entities;

namespace TheBoys.Infrastructure.Persistence.Configurations;

public sealed class GenerationStageConfiguration : IEntityTypeConfiguration<GenerationStage>
{
    public void Configure(EntityTypeBuilder<GenerationStage> builder)
    {
        builder.ToTable("GenerationStages", t =>
        {
            t.HasCheckConstraint("CK_GenerationStages_Stage", "[Stage] IN ('Generate','Resize','Convert','Analyze','Repair')");
            t.HasCheckConstraint("CK_GenerationStages_AttemptNo", "[AttemptNo] > 0");
            t.HasCheckConstraint("CK_GenerationStages_Status", "[Status] IN ('Queued','Submitting','Running','Succeeded','Failed','NeedsAttention')");
            t.HasCheckConstraint("CK_GenerationStages_PollFailureCount", "[PollFailureCount] >= 0");
            t.HasCheckConstraint("CK_GenerationStages_ProviderCredits", "[ProviderCredits] IS NULL OR [ProviderCredits] >= 0");
            t.HasCheckConstraint("CK_GenerationStages_ActualCostVnd", "[ActualCostVnd] IS NULL OR [ActualCostVnd] >= 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Stage)
            .HasMaxLength(20)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.AttemptNo)
            .IsRequired();

        builder.Property(x => x.Provider)
            .HasMaxLength(30)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.ProviderAccountScope)
            .HasMaxLength(100)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.ProviderTaskId)
            .HasMaxLength(150)
            .IsUnicode(false);

        builder.Property(x => x.Status)
            .HasMaxLength(30)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.Progress)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.SubmissionPending)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(x => x.PollFailureCount)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.NextPollAtUtc)
            .HasPrecision(3);

        builder.Property(x => x.LeaseOwner)
            .HasMaxLength(100)
            .IsUnicode(false);

        builder.Property(x => x.LeaseExpiresAtUtc)
            .HasPrecision(3);

        builder.Property(x => x.ProviderCredits)
            .HasPrecision(18, 4);

        builder.Property(x => x.ActualCostVnd)
            .HasPrecision(18, 2);

        builder.Property(x => x.ErrorCode)
            .HasMaxLength(100)
            .IsUnicode(false);

        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(2000);

        builder.Property(x => x.CreatedAtUtc)
            .HasPrecision(3)
            .IsRequired();

        builder.Property(x => x.CompletedAtUtc)
            .HasPrecision(3);

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        builder.HasIndex(x => new { x.JobId, x.Stage, x.AttemptNo })
            .HasDatabaseName("UX_GenerationStages_Job_Stage_Attempt")
            .IsUnique();

        builder.HasIndex(x => new { x.JobId, x.Stage })
            .HasDatabaseName("UX_Stages_Active")
            .IsUnique()
            .HasFilter("[IsActive] = 1");

        builder.HasIndex(x => new { x.Provider, x.ProviderAccountScope, x.ProviderTaskId })
            .HasDatabaseName("UX_Stages_ProviderTask")
            .IsUnique()
            .HasFilter("[ProviderTaskId] IS NOT NULL");

        builder.HasIndex(x => new { x.Status, x.NextPollAtUtc })
            .HasDatabaseName("IX_Stages_Queue");

        builder.HasIndex(x => x.JobId)
            .HasDatabaseName("IX_GenerationStages_JobId");

        builder.HasOne(x => x.Job)
            .WithMany(j => j.Stages)
            .HasForeignKey(x => x.JobId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

