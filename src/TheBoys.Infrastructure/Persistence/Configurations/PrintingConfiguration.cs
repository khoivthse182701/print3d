using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TheBoys.Domain.Entities;

namespace TheBoys.Infrastructure.Persistence.Configurations;

public sealed class PrintingTaskConfiguration : IEntityTypeConfiguration<GenerationTask>
{
    public void Configure(EntityTypeBuilder<GenerationTask> b)
    {
        b.Property(x => x.Provider).HasMaxLength(30);
        b.Property(x => x.AiModel).HasMaxLength(50);
        b.Property(x => x.PrinterType).HasMaxLength(20);
        b.Property(x => x.ClientRequestId).HasMaxLength(64);
        b.Property(x => x.RequestFingerprint).HasMaxLength(64);
        b.HasIndex(x => new { x.UserId, x.ClientRequestId }).IsUnique();
        b.HasIndex(x => new { x.Status, x.NextPollAtUtc });
        b.Property(x => x.ResizeTaskId).HasMaxLength(150);
        b.Property(x => x.ConvertTaskId).HasMaxLength(150);
        b.Property(x => x.AnalysisTaskId).HasMaxLength(150);
        b.Property(x => x.ResizedModelUrl).HasMaxLength(4000);
        b.Property(x => x.ReviewNote).HasMaxLength(1000);
        b.Property(x => x.PrintabilityStatus).HasMaxLength(20);
    }
}

public sealed class PrintingOrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> b)
    {
        b.Property(x => x.ClientRequestId).HasMaxLength(64);
        b.Property(x => x.RequestFingerprint).HasMaxLength(64);
        b.HasIndex(x => new { x.UserId, x.ClientRequestId }).IsUnique();
        b.Property(x => x.WidthMmSnapshot).HasPrecision(10, 2);
        b.Property(x => x.DepthMmSnapshot).HasPrecision(10, 2);
        b.Property(x => x.HeightMmSnapshot).HasPrecision(10, 2);
        b.Property(x => x.MaterialSnapshot).HasMaxLength(50);
    }
}
