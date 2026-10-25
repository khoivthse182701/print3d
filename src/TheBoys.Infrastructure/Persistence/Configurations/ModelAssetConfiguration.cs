using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TheBoys.Domain.Entities;

namespace TheBoys.Infrastructure.Persistence.Configurations;

public sealed class ModelAssetConfiguration : IEntityTypeConfiguration<ModelAsset>
{
    public void Configure(EntityTypeBuilder<ModelAsset> builder)
    {
        builder.ToTable("ModelAssets", t =>
        {
            t.HasCheckConstraint("CK_ModelAssets_Format", "[Format] IN ('GLB','STL','PNG','JPEG','WEBP','3MF')");
            t.HasCheckConstraint("CK_ModelAssets_Purpose", "[Purpose] IN ('Preview','Print','Thumbnail')");
            t.HasCheckConstraint("CK_ModelAssets_ByteLength", "[ByteLength] > 0");
            t.HasCheckConstraint("CK_ModelAssets_CoordinateUnit", "[CoordinateUnit] IN ('mm','m','pixel')");
            t.HasCheckConstraint("CK_ModelAssets_UpAxis", "[UpAxis] IS NULL OR [UpAxis] IN ('X','Y','Z')");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Format)
            .HasMaxLength(10)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.Purpose)
            .HasMaxLength(20)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.StorageKey)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.Sha256)
            .HasMaxLength(32)
            .IsFixedLength()
            .IsRequired();

        builder.Property(x => x.ByteLength)
            .IsRequired();

        builder.Property(x => x.CoordinateUnit)
            .HasMaxLength(10)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.UpAxis)
            .HasMaxLength(10)
            .IsUnicode(false);

        builder.Property(x => x.PurgedAtUtc)
            .HasPrecision(3);

        builder.Property(x => x.RetainUntilUtc)
            .HasPrecision(3);

        builder.Property(x => x.LegalHold)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasPrecision(3)
            .IsRequired();

        builder.HasIndex(x => x.StorageKey)
            .HasDatabaseName("UX_ModelAssets_StorageKey")
            .IsUnique();

        builder.HasIndex(x => new { x.VersionId, x.Format, x.Purpose })
            .HasDatabaseName("UX_ModelAssets_Version_Format_Purpose")
            .IsUnique();

        builder.HasIndex(x => x.VersionId)
            .HasDatabaseName("IX_ModelAssets_VersionId");

        builder.HasOne(x => x.Version)
            .WithMany(v => v.Assets)
            .HasForeignKey(x => x.VersionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

