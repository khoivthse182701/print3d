using TheBoys.Domain.Common;
using TheBoys.Domain.Enums;

namespace TheBoys.Domain.Entities;

public sealed class CreditLedgerEntry : BaseEntity
{
    public Guid UserId { get; private set; }
    public Guid? GenerationTaskId { get; private set; }
    public CreditBucket Bucket { get; private set; }
    public int Delta { get; private set; }
    public CreditLedgerKind Kind { get; private set; }
    public Guid? ReversesEntryId { get; private set; }
    public string OperationKey { get; private set; } = default!;
    public Guid? ActorUserId { get; private set; }
    public string Reason { get; private set; } = default!;
    public DateTime CreatedAtUtc { get; private set; }

    // Navigations
    public CreditAccount? Account { get; private set; }
    public GenerationTask? GenerationTask { get; private set; }
    public CreditLedgerEntry? ReversesEntry { get; private set; }
    public User? ActorUser { get; private set; }

    private CreditLedgerEntry() { }

    public static CreditLedgerEntry CreateOpeningBalance(Guid userId, CreditBucket bucket, int amount, string operationKey, string reason, DateTime utcNow)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "Delta opening balance phải lớn hơn 0.");
        return new CreditLedgerEntry
        {
            UserId = userId,
            Bucket = bucket,
            Delta = amount,
            Kind = CreditLedgerKind.OpeningBalance,
            OperationKey = operationKey,
            Reason = reason,
            CreatedAtUtc = utcNow
        };
    }

    public static CreditLedgerEntry CreateTrialGrant(Guid userId, int amount, string operationKey, Guid? actorUserId, string reason, DateTime utcNow)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "Delta trial grant phải lớn hơn 0.");
        return new CreditLedgerEntry
        {
            UserId = userId,
            Bucket = CreditBucket.Trial,
            Delta = amount,
            Kind = CreditLedgerKind.TrialGrant,
            OperationKey = operationKey,
            ActorUserId = actorUserId,
            Reason = reason,
            CreatedAtUtc = utcNow
        };
    }

    public static CreditLedgerEntry CreateGenerationDebit(Guid userId, Guid generationTaskId, CreditBucket bucket, string operationKey, DateTime utcNow)
    {
        return new CreditLedgerEntry
        {
            UserId = userId,
            GenerationTaskId = generationTaskId,
            Bucket = bucket,
            Delta = -1,
            Kind = CreditLedgerKind.GenerationDebit,
            OperationKey = operationKey,
            Reason = $"Trừ 1 credit cho tác vụ sinh 3D: {generationTaskId}",
            CreatedAtUtc = utcNow
        };
    }

    public static CreditLedgerEntry CreateGenerationRefund(Guid userId, Guid generationTaskId, CreditBucket bucket, Guid reversesDebitEntryId, string operationKey, DateTime utcNow)
    {
        return new CreditLedgerEntry
        {
            UserId = userId,
            GenerationTaskId = generationTaskId,
            Bucket = bucket,
            Delta = 1,
            Kind = CreditLedgerKind.GenerationRefund,
            ReversesEntryId = reversesDebitEntryId,
            OperationKey = operationKey,
            Reason = $"Hoàn 1 credit do tác vụ sinh 3D thất bại: {generationTaskId}",
            CreatedAtUtc = utcNow
        };
    }

    public static CreditLedgerEntry CreateAdminAdjustment(Guid userId, CreditBucket bucket, int amount, Guid actorUserId, string operationKey, string reason, DateTime utcNow)
    {
        if (amount == 0) throw new ArgumentOutOfRangeException(nameof(amount), "Delta adjustment không được bằng 0.");
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Lý do admin adjustment là bắt buộc.", nameof(reason));

        return new CreditLedgerEntry
        {
            UserId = userId,
            Bucket = bucket,
            Delta = amount,
            Kind = CreditLedgerKind.AdminAdjustment,
            ActorUserId = actorUserId,
            OperationKey = operationKey,
            Reason = reason.Trim(),
            CreatedAtUtc = utcNow
        };
    }
}

