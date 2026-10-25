using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TheBoys.Domain.Entities;

namespace TheBoys.Infrastructure.Persistence.Configurations;

public sealed class ModelProjectConfiguration : IEntityTypeConfiguration<ModelProject>
{
    public void Configure(EntityTypeBuilder<ModelProject> builder)
    {
        builder.ToTable("ModelProjects");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasPrecision(3)
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .HasPrecision(3);

        builder.Property(x => x.ArchivedAtUtc)
            .HasPrecision(3);

        builder.HasIndex(x => x.UserId)
            .HasDatabaseName("IX_ModelProjects_UserId");

        builder.HasIndex(x => new { x.Id, x.UserId })
            .HasDatabaseName("UX_ModelProjects_Id_UserId")
            .IsUnique();

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

