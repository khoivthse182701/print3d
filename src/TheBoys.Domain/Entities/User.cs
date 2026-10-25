using TheBoys.Domain.Common;
using TheBoys.Domain.Enums;

namespace TheBoys.Domain.Entities;

public sealed class User : BaseAuditableEntity
{
    private string _email = default!;
    public string Email
    {
        get => _email;
        set
        {
            _email = value;
            if (!string.IsNullOrWhiteSpace(value) && string.IsNullOrEmpty(NormalizedEmail))
            {
                NormalizedEmail = value.Trim().ToUpperInvariant();
            }
        }
    }
    public string NormalizedEmail { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public string FullName { get; set; } = default!;
    public string? PhoneNumber { get; set; }
    public UserRole Role { get; set; } = UserRole.Customer;
    public AccountStatus AccountStatus { get; set; } = AccountStatus.Unverified;
    public DateTime? EmailVerifiedAtUtc { get; set; }
    public int FailedLoginAttempts { get; set; } = 0;
    public DateTime? LockoutEndUtc { get; set; }
    public string? DeviceFingerprint { get; set; }
    public bool IsPhoneVerified { get; set; }

    // Gộp từ UserCredits (Hạn mức 2 lượt tạo AI miễn phí theo CP3)
    public int FreeTrialRemaining { get; set; } = 2;
    public int PurchasedRemaining { get; set; } = 0;
    public int TotalUsed { get; set; } = 0;

    // Navigation properties
    public CreditAccount? CreditAccount { get; set; }
    public ICollection<CreditLedgerEntry> LedgerEntries { get; set; } = new List<CreditLedgerEntry>();
    public ICollection<RefreshSession> RefreshSessions { get; set; } = new List<RefreshSession>();
    public ICollection<IdentityToken> IdentityTokens { get; set; } = new List<IdentityToken>();
    public ICollection<ConsentRecord> ConsentRecords { get; set; } = new List<ConsentRecord>();
    public ICollection<GenerationTask> GenerationTasks { get; set; } = new List<GenerationTask>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<PrintReview> PrintReviews { get; set; } = new List<PrintReview>();
    public ICollection<CustomerApproval> CustomerApprovals { get; set; } = new List<CustomerApproval>();
    public ICollection<Quote> Quotes { get; set; } = new List<Quote>();
    public ICollection<PartnerAssignment> AssignedPartnerAssignments { get; set; } = new List<PartnerAssignment>();
    public ICollection<ShipmentEvent> ShipmentEvents { get; set; } = new List<ShipmentEvent>();
    public ICollection<DefectReport> DefectReports { get; set; } = new List<DefectReport>();
    public ICollection<DefectReport> ResolvedDefectReports { get; set; } = new List<DefectReport>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public ICollection<PrivacyRequest> PrivacyRequests { get; set; } = new List<PrivacyRequest>();
    public ICollection<PrivacyRequest> HandledPrivacyRequests { get; set; } = new List<PrivacyRequest>();

    public bool IsLockedOut(DateTime utcNow)
    {
        if (AccountStatus == AccountStatus.Locked) return true;
        return LockoutEndUtc.HasValue && LockoutEndUtc.Value > utcNow;
    }

    public void RecordFailedLogin(int maxFailedAttempts, int lockoutMinutes, DateTime utcNow)
    {
        FailedLoginAttempts++;
        if (FailedLoginAttempts >= maxFailedAttempts)
        {
            LockoutEndUtc = utcNow.AddMinutes(lockoutMinutes);
            AccountStatus = AccountStatus.Locked;
        }
    }

    public void ResetFailedLogins()
    {
        FailedLoginAttempts = 0;
        LockoutEndUtc = null;
        if (AccountStatus == AccountStatus.Locked)
            AccountStatus = EmailVerifiedAtUtc.HasValue ? AccountStatus.Active : AccountStatus.Unverified;
    }

    public void VerifyEmail(DateTime utcNow)
    {
        EmailVerifiedAtUtc = utcNow;
        if (AccountStatus == AccountStatus.Unverified)
            AccountStatus = AccountStatus.Active;
    }

    public void Lock(DateTime utcNow)
    {
        AccountStatus = AccountStatus.Locked;
        LockoutEndUtc = utcNow.AddYears(100);
    }

    public void Unlock()
    {
        ResetFailedLogins();
        if (AccountStatus == AccountStatus.Locked)
            AccountStatus = EmailVerifiedAtUtc.HasValue ? AccountStatus.Active : AccountStatus.Unverified;
    }

    public bool HasAvailableCredit() => (FreeTrialRemaining + PurchasedRemaining) > 0;

    public bool DeductCredit()
    {
        if (!HasAvailableCredit()) throw new InvalidOperationException("Không còn lượt tạo model.");
        var purchased = FreeTrialRemaining == 0;
        if (FreeTrialRemaining > 0)
        {
            FreeTrialRemaining--;
        }
        else if (PurchasedRemaining > 0)
        {
            PurchasedRemaining--;
        }
        TotalUsed++;
        return purchased;
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

