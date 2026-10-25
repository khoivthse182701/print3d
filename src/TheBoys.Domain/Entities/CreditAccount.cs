using TheBoys.Domain.Enums;

namespace TheBoys.Domain.Entities;

public sealed class CreditAccount
{
    public Guid UserId { get; private set; }
    public int TrialBalance { get; private set; }
    public int PurchasedBalance { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    // Navigation
    public User? User { get; private set; }

    private CreditAccount() { }

    public static CreditAccount Create(Guid userId, int trialBalance = 1, int purchasedBalance = 0)
    {
        if (trialBalance < 0) throw new ArgumentOutOfRangeException(nameof(trialBalance), "Trial balance không được âm.");
        if (purchasedBalance < 0) throw new ArgumentOutOfRangeException(nameof(purchasedBalance), "Purchased balance không được âm.");

        return new CreditAccount
        {
            UserId = userId,
            TrialBalance = trialBalance,
            PurchasedBalance = purchasedBalance
        };
    }

    public int TotalBalance => TrialBalance + PurchasedBalance;

    public bool CanDebit() => TotalBalance > 0;

    public CreditBucket Debit()
    {
        if (!CanDebit())
            throw new InvalidOperationException("Không đủ credit để thực hiện tác vụ.");

        if (TrialBalance > 0)
        {
            TrialBalance--;
            return CreditBucket.Trial;
        }

        PurchasedBalance--;
        return CreditBucket.Purchased;
    }

    public void GrantTrial(int amount)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "Số lượng grant phải lớn hơn 0.");
        TrialBalance += amount;
    }

    public void GrantPurchased(int amount)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "Số lượng grant phải lớn hơn 0.");
        PurchasedBalance += amount;
    }

    public void Reverse(CreditBucket bucket, int amount = 1)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "Số lượng hoàn phải lớn hơn 0.");

        if (bucket == CreditBucket.Trial)
        {
            TrialBalance += amount;
        }
        else
        {
            PurchasedBalance += amount;
        }
    }
}

