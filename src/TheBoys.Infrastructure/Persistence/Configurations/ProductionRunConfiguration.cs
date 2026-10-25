using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TheBoys.Domain.Entities;

namespace TheBoys.Infrastructure.Persistence.Configurations;

public sealed class ProductionRunConfiguration : IEntityTypeConfiguration<ProductionRun>
{
    public void Configure(EntityTypeBuilder<ProductionRun> builder)
    {
        builder.ToTable("ProductionRuns", t =>
        {
            t.HasCheckConstraint("CK_ProductionRuns_Metrics", "[RunNo] > 0 AND [Quantity] > 0 AND ([ActualCostVnd] IS NULL OR [ActualCostVnd] >= 0)");
            t.HasCheckConstraint("CK_ProductionRuns_Status", "[Status] IN ('Printing','PostProcessing','QualityCheck','Passed','Failed','Cancelled')");
            t.HasCheckConstraint("CK_ProductionRuns_QcPassed", "([Status] <> 'Passed' OR ([QcEvidenceStorageKey] IS NOT NULL AND [QcNote] IS NOT NULL AND [CompletedAtUtc] IS NOT NULL))");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(30)
            .HasConversion<string>();

        builder.Property(x => x.ActualCostVnd)
            .HasPrecision(18, 2);

        builder.Property(x => x.QcEvidenceStorageKey)
            .HasMaxLength(500);

        builder.Property(x => x.QcNote)
            .HasMaxLength(1000);

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        builder.HasOne(x => x.Assignment)
            .WithMany(a => a.ProductionRuns)
            .HasForeignKey(x => x.AssignmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.AssignmentId, x.RunNo })
            .IsUnique();
    }
}

