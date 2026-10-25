using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TheBoys.Domain.Entities;

namespace TheBoys.Infrastructure.Persistence.Configurations;

public sealed class ConsentRecordConfiguration : IEntityTypeConfiguration<ConsentRecord>
{
    public void Configure(EntityTypeBuilder<ConsentRecord> builder)
    {
        builder.ToTable("ConsentRecords");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Purpose)
            .HasMaxLength(40)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.PolicyVersion)
            .HasMaxLength(40)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.Granted)
            .IsRequired();

        builder.Property(x => x.RecordedAtUtc)
            .HasPrecision(3)
            .IsRequired();

        builder.HasOne(x => x.User)
            .WithMany(x => x.ConsentRecords)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.UserId, x.Purpose, x.RecordedAtUtc })
            .HasDatabaseName("IX_ConsentRecords_UserPurposeTime");
    }
}

