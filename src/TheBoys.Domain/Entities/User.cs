using TheBoys.Domain.Common;
using TheBoys.Domain.Enums;

namespace TheBoys.Domain.Entities;

public sealed class User : BaseAuditableEntity
{
    public string Email { get; set; } = default!;
    public string? PasswordHash { get; set; }
    public string FullName { get; set; } = default!;
    public string? PhoneNumber { get; set; }
    public UserRole Role { get; set; } = UserRole.Customer;
    public string? DeviceFingerprint { get; set; }
    public bool IsPhoneVerified { get; set; }

    // Gộp từ UserCredits (Hạn mức 2 lượt tạo AI miễn phí theo CP3)
    public int FreeTrialRemaining { get; set; } = 2;
    public int PurchasedRemaining { get; set; } = 0;
    public int TotalUsed { get; set; } = 0;

    // Navigation properties
    public ICollection<GenerationTask> GenerationTasks { get; set; } = new List<GenerationTask>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();

    public bool HasAvailableCredit() => (FreeTrialRemaining + PurchasedRemaining) > 0;

    public void DeductCredit()
    {
        if (FreeTrialRemaining > 0)
        {
            FreeTrialRemaining--;
        }
        else if (PurchasedRemaining > 0)
        {
            PurchasedRemaining--;
        }
        TotalUsed++;
    }

    public void RefundCredit(bool wasPurchased = false)
    {
        if (wasPurchased)
        {
            PurchasedRemaining++;
        }
        else
        {
            FreeTrialRemaining++;
        }

        if (TotalUsed > 0) TotalUsed--;
    }
}
