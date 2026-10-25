using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TheBoys.Domain.Entities;

namespace TheBoys.Infrastructure.Persistence.Configurations;

public sealed class DefectEvidenceConfiguration : IEntityTypeConfiguration<DefectEvidence>
{
    public void Configure(EntityTypeBuilder<DefectEvidence> builder)
    {
        builder.ToTable("DefectEvidence");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.StorageKey)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.Sha256)
            .IsRequired()
            .HasColumnType("binary(32)");

        builder.HasOne(x => x.Report)
            .WithMany(r => r.Evidence)
            .HasForeignKey(x => x.ReportId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.StorageKey)
            .IsUnique();
    }
}

