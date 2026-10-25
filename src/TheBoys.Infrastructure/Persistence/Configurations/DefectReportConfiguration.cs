using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TheBoys.Domain.Entities;

namespace TheBoys.Infrastructure.Persistence.Configurations;

public sealed class DefectReportConfiguration : IEntityTypeConfiguration<DefectReport>
{
    public void Configure(EntityTypeBuilder<DefectReport> builder)
    {
        builder.ToTable("DefectReports", t =>
        {
            t.HasCheckConstraint("CK_DefectReports_Status", "[Status] IN ('Open','AwaitingEvidence','Investigating','ReprintApproved','RefundApproved','Rejected','Resolved')");
            t.HasCheckConstraint("CK_DefectReports_SingleResolution", "([RefundId] IS NULL OR [ReprintRunId] IS NULL)");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Reason)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(30)
            .HasConversion<string>();

        builder.Property(x => x.ResolutionNote)
            .HasMaxLength(1000);

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        builder.HasOne(x => x.Order)
            .WithMany(o => o.DefectReports)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.User)
            .WithMany(u => u.DefectReports)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ResolvedByUser)
            .WithMany(u => u.ResolvedDefectReports)
            .HasForeignKey(x => x.ResolvedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Refund)
            .WithMany(r => r.DefectReports)
            .HasForeignKey(x => x.RefundId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ReprintRun)
            .WithOne(pr => pr.DefectReportReprint)
            .HasForeignKey<DefectReport>(x => x.ReprintRunId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.UserId, x.ClientRequestId })
            .IsUnique();
    }
}

