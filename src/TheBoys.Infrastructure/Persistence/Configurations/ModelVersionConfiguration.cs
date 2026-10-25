using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TheBoys.Domain.Entities;

namespace TheBoys.Infrastructure.Persistence.Configurations;

public sealed class ModelVersionConfiguration : IEntityTypeConfiguration<ModelVersion>
{
    public void Configure(EntityTypeBuilder<ModelVersion> builder)
    {
        builder.ToTable("ModelVersions", t =>
        {
            t.HasCheckConstraint("CK_ModelVersions_VersionNo", "[VersionNo] > 0");
            t.HasCheckConstraint("CK_ModelVersions_WidthMm", "[WidthMm] > 0");
            t.HasCheckConstraint("CK_ModelVersions_DepthMm", "[DepthMm] > 0");
            t.HasCheckConstraint("CK_ModelVersions_HeightMm", "[HeightMm] > 0");
            t.HasCheckConstraint("CK_ModelVersions_VolumeCm3", "[VolumeCm3] IS NULL OR [VolumeCm3] > 0");
            t.HasCheckConstraint("CK_ModelVersions_AnalysisCoverage", "[AnalysisCoverage] IN ('GeometryOnly','GeometryAndThickness','ManualSlicer')");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.VersionNo)
            .IsRequired();

        builder.Property(x => x.GeometryHash)
            .HasMaxLength(32)
            .IsFixedLength()
            .IsRequired();

        builder.Property(x => x.WidthMm)
            .HasPrecision(10, 3)
            .IsRequired();

        builder.Property(x => x.DepthMm)
            .HasPrecision(10, 3)
            .IsRequired();

        builder.Property(x => x.HeightMm)
            .HasPrecision(10, 3)
            .IsRequired();

        builder.Property(x => x.VolumeCm3)
            .HasPrecision(18, 4);

        builder.Property(x => x.IsWatertight)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(x => x.PrintabilityStatus)
            .HasMaxLength(40)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.AnalysisCoverage)
            .HasMaxLength(40)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.AnalysisJson);

        builder.Property(x => x.IsPublished)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.PublishedAtUtc)
            .HasPrecision(3)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasPrecision(3)
            .IsRequired();

        builder.HasIndex(x => new { x.ProjectId, x.VersionNo })
            .HasDatabaseName("UX_ModelVersions_Project_VersionNo")
            .IsUnique();

        builder.HasIndex(x => x.ProjectId)
            .HasDatabaseName("IX_ModelVersions_ProjectId");

        builder.HasIndex(x => x.SourceJobId)
            .HasDatabaseName("IX_ModelVersions_SourceJobId");

        builder.HasIndex(x => x.ParentVersionId)
            .HasDatabaseName("IX_ModelVersions_ParentVersionId");

        builder.HasOne(x => x.Project)
            .WithMany(p => p.Versions)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.SourceJob)
            .WithMany(j => j.ProducedVersions)
            .HasForeignKey(x => x.SourceJobId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ParentVersion)
            .WithMany(v => v.ChildVersions)
            .HasForeignKey(x => x.ParentVersionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

