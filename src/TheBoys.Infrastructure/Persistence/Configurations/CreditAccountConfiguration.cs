using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TheBoys.Domain.Entities;

namespace TheBoys.Infrastructure.Persistence.Configurations;

public sealed class CreditAccountConfiguration : IEntityTypeConfiguration<CreditAccount>
{
    public void Configure(EntityTypeBuilder<CreditAccount> builder)
    {
        builder.ToTable("CreditAccounts", t =>
        {
            t.HasCheckConstraint("CK_CreditAccounts_TrialBalanceNonNegative", "[TrialBalance] >= 0");
            t.HasCheckConstraint("CK_CreditAccounts_PurchasedBalanceNonNegative", "[PurchasedBalance] >= 0");
        });

        builder.HasKey(x => x.UserId);

        builder.Property(x => x.TrialBalance)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.PurchasedBalance)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        builder.HasOne(x => x.User)
            .WithOne(x => x.CreditAccount)
            .HasForeignKey<CreditAccount>(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

