using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TheBoys.Domain.Entities;

namespace TheBoys.Infrastructure.Persistence.Configurations;

public sealed class PartnerAssignmentConfiguration : IEntityTypeConfiguration<PartnerAssignment>
{
    public void Configure(EntityTypeBuilder<PartnerAssignment> builder)
    {
        builder.ToTable("PartnerAssignments", t =>
        {
            t.HasCheckConstraint("CK_PartnerAssignments_Status", "[Status] IN ('Offered','Accepted','Rejected','Expired','Completed','Cancelled')");
            t.HasCheckConstraint("CK_PartnerAssignments_Cost", "[PartnerCostSnapshotVnd] >= 0");
            t.HasCheckConstraint("CK_PartnerAssignments_ActiveState", "(([Status] IN ('Offered','Accepted') AND [IsActive] = 1) OR ([Status] NOT IN ('Offered','Accepted') AND [IsActive] = 0))");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion<string>();

        builder.Property(x => x.PartnerCostSnapshotVnd)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.DecisionNote)
            .HasMaxLength(500);

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        builder.HasOne(x => x.Order)
            .WithMany(o => o.PartnerAssignments)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.PartnerLab)
            .WithMany(l => l.PartnerAssignments)
            .HasForeignKey(x => x.PartnerLabId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.AssignedByUser)
            .WithMany(u => u.AssignedPartnerAssignments)
            .HasForeignKey(x => x.AssignedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.OrderId)
            .IsUnique()
            .HasDatabaseName("UX_Assignment_ActiveOrder")
            .HasFilter("[IsActive] = 1");
    }
}

