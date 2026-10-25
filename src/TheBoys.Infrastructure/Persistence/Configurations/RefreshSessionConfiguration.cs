using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TheBoys.Domain.Entities;

namespace TheBoys.Infrastructure.Persistence.Configurations;

public sealed class RefreshSessionConfiguration : IEntityTypeConfiguration<RefreshSession>
{
    public void Configure(EntityTypeBuilder<RefreshSession> builder)
    {
        builder.ToTable("RefreshSessions", t =>
        {
            t.HasCheckConstraint("CK_RefreshSessions_ExpiryAfterCreated", "[ExpiresAtUtc] > [CreatedAtUtc]");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.FamilyId)
            .IsRequired();

        builder.Property(x => x.TokenHash)
            .HasMaxLength(32)
            .IsFixedLength()
            .IsRequired();

        builder.Property(x => x.ExpiresAtUtc)
            .HasPrecision(3)
            .IsRequired();

        builder.Property(x => x.RevokedAtUtc)
            .HasPrecision(3);

        builder.Property(x => x.CreatedAtUtc)
            .HasPrecision(3)
            .IsRequired();

        builder.HasOne(x => x.User)
            .WithMany(x => x.RefreshSessions)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ReplacedBy)
            .WithMany()
            .HasForeignKey(x => x.ReplacedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.TokenHash)
            .HasDatabaseName("UX_RefreshSessions_TokenHash")
            .IsUnique();

        builder.HasIndex(x => new { x.UserId, x.FamilyId })
            .HasDatabaseName("IX_RefreshSessions_UserFamily");
    }
}

