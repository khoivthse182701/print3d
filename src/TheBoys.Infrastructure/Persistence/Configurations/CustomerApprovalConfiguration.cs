using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TheBoys.Domain.Entities;

namespace TheBoys.Infrastructure.Persistence.Configurations;

public sealed class CustomerApprovalConfiguration : IEntityTypeConfiguration<CustomerApproval>
{
    public void Configure(EntityTypeBuilder<CustomerApproval> builder)
    {
        builder.ToTable("CustomerApprovals");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId)
            .IsRequired();

        builder.Property(x => x.VersionId)
            .IsRequired();

        builder.Property(x => x.ConfigurationHash)
            .HasMaxLength(32)
            .IsFixedLength()
            .IsRequired();

        builder.Property(x => x.ConfigurationJson)
            .IsRequired();

        builder.Property(x => x.PolicyVersion)
            .HasMaxLength(40)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasPrecision(3)
            .IsRequired();

        builder.HasOne(x => x.User)
            .WithMany(u => u.CustomerApprovals)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Version)
            .WithMany(v => v.CustomerApprovals)
            .HasForeignKey(x => x.VersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.UserId, x.VersionId, x.ConfigurationHash, x.CreatedAtUtc })
            .HasDatabaseName("IX_CustomerApprovals_User_Version_Hash");

        builder.HasIndex(x => x.VersionId)
            .HasDatabaseName("IX_CustomerApprovals_VersionId");
    }
}

