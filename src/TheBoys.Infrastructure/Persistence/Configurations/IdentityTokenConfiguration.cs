using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TheBoys.Domain.Entities;

namespace TheBoys.Infrastructure.Persistence.Configurations;

public sealed class IdentityTokenConfiguration : IEntityTypeConfiguration<IdentityToken>
{
    public void Configure(EntityTypeBuilder<IdentityToken> builder)
    {
        builder.ToTable("IdentityTokens", t =>
        {
            t.HasCheckConstraint("CK_IdentityTokens_ExpiryAfterCreated", "[ExpiresAtUtc] > [CreatedAtUtc]");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Purpose)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.TokenHash)
            .HasMaxLength(32)
            .IsFixedLength()
            .IsRequired();

        builder.Property(x => x.ExpiresAtUtc)
            .HasPrecision(3)
            .IsRequired();

        builder.Property(x => x.ConsumedAtUtc)
            .HasPrecision(3);

        builder.Property(x => x.CreatedAtUtc)
            .HasPrecision(3)
            .IsRequired();

        builder.HasOne(x => x.User)
            .WithMany(x => x.IdentityTokens)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.TokenHash)
            .HasDatabaseName("UX_IdentityTokens_TokenHash")
            .IsUnique();
    }
}

