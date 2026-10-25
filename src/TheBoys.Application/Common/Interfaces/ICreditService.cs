using TheBoys.Application.Common.Models;
using TheBoys.Domain.Enums;

namespace TheBoys.Application.Common.Interfaces;

public sealed record CreditDebitReceipt(Guid UserId, Guid GenerationTaskId, CreditBucket Bucket, int RemainingTrial, int RemainingPurchased);
public sealed record CreditRefundReceipt(Guid UserId, Guid GenerationTaskId, CreditBucket Bucket, int RemainingTrial, int RemainingPurchased);
public sealed record CreditGrantReceipt(Guid UserId, int Amount, int RemainingTrial, int RemainingPurchased);
public sealed record CreditBalanceDto(int TrialBalance, int PurchasedBalance, int TotalBalance);

public interface ICreditService
{
    Task<Result<CreditDebitReceipt>> DebitForGenerationAsync(Guid userId, Guid generationTaskId, string operationKey, CancellationToken ct);
    Task<Result<CreditRefundReceipt>> RefundForGenerationAsync(Guid userId, Guid generationTaskId, string operationKey, CancellationToken ct);
    Task<Result<CreditGrantReceipt>> AdminGrantAsync(Guid targetUserId, Guid adminUserId, int amount, string reason, string idempotencyKey, CancellationToken ct);
    Task<Result<CreditGrantReceipt>> AddPurchasedCreditsAsync(Guid userId, int amount, string operationKey, string reason, CancellationToken ct);
    Task<CreditBalanceDto> GetBalanceAsync(Guid userId, CancellationToken ct);
}
