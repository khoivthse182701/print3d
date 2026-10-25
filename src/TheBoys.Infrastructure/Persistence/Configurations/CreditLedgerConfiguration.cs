using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TheBoys.Domain.Entities;

namespace TheBoys.Infrastructure.Persistence.Configurations;

public sealed class CreditLedgerConfiguration : IEntityTypeConfiguration<CreditLedgerEntry>
{
    public void Configure(EntityTypeBuilder<CreditLedgerEntry> builder)
    {
        builder.ToTable("CreditLedger", t =>
        {
            t.HasCheckConstraint("CK_CreditLedger_DeltaNonZero", "[Delta] <> 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Bucket)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.Delta)
            .IsRequired();

        builder.Property(x => x.Kind)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.OperationKey)
            .HasMaxLength(150)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.Reason)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasPrecision(3)
            .IsRequired();

        // Foreign keys with DeleteBehavior.Restrict
        builder.HasOne(x => x.Account)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.GenerationTask)
            .WithMany()
            .HasForeignKey(x => x.GenerationTaskId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ReversesEntry)
            .WithMany()
            .HasForeignKey(x => x.ReversesEntryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ActorUser)
            .WithMany()
            .HasForeignKey(x => x.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Constraints and indexes
        builder.HasIndex(x => new { x.UserId, x.OperationKey })
            .HasDatabaseName("UX_Ledger_UserOperation")
            .IsUnique();

        builder.HasIndex(x => x.ReversesEntryId)
            .HasDatabaseName("UX_Ledger_RefundOnce")
            .IsUnique()
            .HasFilter("[ReversesEntryId] IS NOT NULL");

        builder.HasIndex(x => x.GenerationTaskId)
            .HasDatabaseName("UX_Ledger_JobDebit")
            .IsUnique()
            .HasFilter("[Kind] = 'GenerationDebit'");

        builder.HasIndex(x => new { x.UserId, x.CreatedAtUtc })
            .HasDatabaseName("IX_Ledger_UserTime");
    }
}

