using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TheBoys.Domain.Entities;

namespace TheBoys.Infrastructure.Persistence.Configurations;

public sealed class PrivacyRequestConfiguration : IEntityTypeConfiguration<PrivacyRequest>
{
    public void Configure(EntityTypeBuilder<PrivacyRequest> builder)
    {
        builder.ToTable("PrivacyRequests", t =>
        {
            t.HasCheckConstraint("CK_PrivacyRequests_Type", "[RequestType] IN ('DeleteAssets','AnonymizeAccount')");
            t.HasCheckConstraint("CK_PrivacyRequests_Status", "[Status] IN ('Pending','OnHold','Processing','Completed','Rejected')");
            t.HasCheckConstraint("CK_PrivacyRequests_Completed", "([Status] <> 'Completed' OR [CompletedAtUtc] IS NOT NULL)");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId)
            .IsRequired();

        builder.Property(x => x.RequestType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.Reason)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(x => x.DecisionNote)
            .HasMaxLength(1000);

        builder.Property(x => x.HandledByUserId);

        builder.Property(x => x.CreatedAtUtc)
            .HasPrecision(3)
            .IsRequired();

        builder.Property(x => x.CompletedAtUtc)
            .HasPrecision(3);

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        builder.HasOne(x => x.User)
            .WithMany(u => u.PrivacyRequests)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.HandledByUser)
            .WithMany(u => u.HandledPrivacyRequests)
            .HasForeignKey(x => x.HandledByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

