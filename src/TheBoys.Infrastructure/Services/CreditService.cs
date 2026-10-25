using System.Data;
using Microsoft.EntityFrameworkCore;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Application.Common.Models;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;

namespace TheBoys.Infrastructure.Services;

public sealed class CreditService(IApplicationDbContext db, IClock clock) : ICreditService
{
    private const int MaxConcurrencyRetries = 5;

    public async Task<Result<CreditDebitReceipt>> DebitForGenerationAsync(
        Guid userId,
        Guid generationTaskId,
        string operationKey,
        CancellationToken ct)
    {
        for (var attempt = 0; attempt < MaxConcurrencyRetries; attempt++)
        {
            await using var tx = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                // Check if this operationKey was already debited for this user
                var existingEntry = await db.CreditLedger
                    .SingleOrDefaultAsync(x => x.UserId == userId && x.OperationKey == operationKey, ct);

                var account = await db.CreditAccounts
                    .SingleOrDefaultAsync(x => x.UserId == userId, ct);

                if (account == null)
                    return Result<CreditDebitReceipt>.Failure(Error.NotFound("CreditAccountNotFound", "Tài khoản credit không tồn tại."));

                if (existingEntry != null)
                {
                    await tx.RollbackAsync(ct);
                    return Result<CreditDebitReceipt>.Success(new CreditDebitReceipt(
                        userId,
                        generationTaskId,
                        existingEntry.Bucket,
                        account.TrialBalance,
                        account.PurchasedBalance
                    ));
                }

                // Check if this generation job was already debited under another key
                var jobDebit = await db.CreditLedger
                    .SingleOrDefaultAsync(x => x.GenerationTaskId == generationTaskId && x.Kind == CreditLedgerKind.GenerationDebit, ct);

                if (jobDebit != null)
                {
                    await tx.RollbackAsync(ct);
                    return Result<CreditDebitReceipt>.Failure(Error.Conflict("JobAlreadyDebited", "Tác vụ này đã được trừ credit."));
                }

                if (!account.CanDebit())
                {
                    await tx.RollbackAsync(ct);
                    return Result<CreditDebitReceipt>.Failure(Error.PaymentRequired("NoCredit", "Bạn đã hết lượt tạo model."));
                }

                var bucket = account.Debit();
                var ledgerEntry = CreditLedgerEntry.CreateGenerationDebit(
                    userId,
                    generationTaskId,
                    bucket,
                    operationKey,
                    clock.UtcNow
                );

                db.CreditLedger.Add(ledgerEntry);

                // Synchronize legacy User fields for temporary caller backward-compatibility
                var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId, ct);
                if (user != null)
                {
                    user.FreeTrialRemaining = account.TrialBalance;
                    user.PurchasedRemaining = account.PurchasedBalance;
                    user.TotalUsed++;
                }

                var audit = AuditLog.Create(
                    userId,
                    "Credit.Debit",
                    "CreditAccount",
                    userId,
                    operationKey,
                    System.Text.Json.JsonSerializer.Serialize(new { generationTaskId, bucket = bucket.ToString(), delta = -1 }),
                    "Khấu trừ credit cho tác vụ sinh 3D",
                    clock.UtcNow
                );
                db.AuditLogs.Add(audit);

                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                return Result<CreditDebitReceipt>.Success(new CreditDebitReceipt(
                    userId,
                    generationTaskId,
                    bucket,
                    account.TrialBalance,
                    account.PurchasedBalance
                ));
            }
            catch (Exception ex) when (attempt < MaxConcurrencyRetries - 1 && IsTransientOrConcurrency(ex))
            {
                await tx.RollbackAsync(ct);
                (db as DbContext)?.ChangeTracker.Clear();
                await Task.Delay(25 * (attempt + 1), ct);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                (db as DbContext)?.ChangeTracker.Clear();
                throw;
            }
        }

        return Result<CreditDebitReceipt>.Failure(Error.Conflict("ConcurrencyConflict", "Hệ thống đang bận xử lý số dư. Vui lòng thử lại."));
    }

    public async Task<Result<CreditRefundReceipt>> RefundForGenerationAsync(
        Guid userId,
        Guid generationTaskId,
        string operationKey,
        CancellationToken ct)
    {
        for (var attempt = 0; attempt < MaxConcurrencyRetries; attempt++)
        {
            await using var tx = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                // Find original debit entry
                var debitEntry = await db.CreditLedger
                    .SingleOrDefaultAsync(x => x.GenerationTaskId == generationTaskId && x.Kind == CreditLedgerKind.GenerationDebit, ct);

                if (debitEntry == null)
                {
                    await tx.RollbackAsync(ct);
                    return Result<CreditRefundReceipt>.Failure(Error.NotFound("DebitNotFound", "Không tìm thấy lượt trừ credit tương ứng để hoàn."));
                }

                var account = await db.CreditAccounts
                    .SingleOrDefaultAsync(x => x.UserId == userId, ct);

                if (account == null)
                    return Result<CreditRefundReceipt>.Failure(Error.NotFound("CreditAccountNotFound", "Tài khoản credit không tồn tại."));

                // Check if a refund has already been recorded for this debit entry
                var existingRefund = await db.CreditLedger
                    .SingleOrDefaultAsync(x => x.ReversesEntryId == debitEntry.Id, ct);

                if (existingRefund != null)
                {
                    await tx.RollbackAsync(ct);
                    return Result<CreditRefundReceipt>.Success(new CreditRefundReceipt(
                        userId,
                        generationTaskId,
                        existingRefund.Bucket,
                        account.TrialBalance,
                        account.PurchasedBalance
                    ));
                }

                account.Reverse(debitEntry.Bucket, 1);

                var refundEntry = CreditLedgerEntry.CreateGenerationRefund(
                    userId,
                    generationTaskId,
                    debitEntry.Bucket,
                    debitEntry.Id,
                    operationKey,
                    clock.UtcNow
                );

                db.CreditLedger.Add(refundEntry);

                // Synchronize legacy User fields for temporary caller backward-compatibility
                var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId, ct);
                if (user != null)
                {
                    user.FreeTrialRemaining = account.TrialBalance;
                    user.PurchasedRemaining = account.PurchasedBalance;
                    if (user.TotalUsed > 0) user.TotalUsed--;
                }

                var audit = AuditLog.Create(
                    userId,
                    "Credit.Refund",
                    "CreditAccount",
                    userId,
                    operationKey,
                    System.Text.Json.JsonSerializer.Serialize(new { generationTaskId, bucket = debitEntry.Bucket.ToString(), delta = 1, reversesEntryId = debitEntry.Id }),
                    "Hoàn trả credit do tác vụ sinh 3D thất bại",
                    clock.UtcNow
                );
                db.AuditLogs.Add(audit);

                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                return Result<CreditRefundReceipt>.Success(new CreditRefundReceipt(
                    userId,
                    generationTaskId,
                    debitEntry.Bucket,
                    account.TrialBalance,
                    account.PurchasedBalance
                ));
            }
            catch (Exception ex) when (attempt < MaxConcurrencyRetries - 1 && IsTransientOrConcurrency(ex))
            {
                await tx.RollbackAsync(ct);
                (db as DbContext)?.ChangeTracker.Clear();
                await Task.Delay(25 * (attempt + 1), ct);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                (db as DbContext)?.ChangeTracker.Clear();
                throw;
            }
        }

        return Result<CreditRefundReceipt>.Failure(Error.Conflict("ConcurrencyConflict", "Hệ thống đang bận xử lý số dư. Vui lòng thử lại."));
    }

    public async Task<Result<CreditGrantReceipt>> AdminGrantAsync(
        Guid targetUserId,
        Guid adminUserId,
        int amount,
        string reason,
        string idempotencyKey,
        CancellationToken ct)
    {
        if (amount <= 0)
            return Result<CreditGrantReceipt>.Failure(Error.Validation("InvalidAmount", "Số credit cấp phải lớn hơn 0."));

        if (string.IsNullOrWhiteSpace(reason))
            return Result<CreditGrantReceipt>.Failure(Error.Validation("ReasonRequired", "Lý do cấp credit là bắt buộc."));

        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return Result<CreditGrantReceipt>.Failure(Error.Validation("IdempotencyKeyRequired", "Idempotency-Key là bắt buộc."));

        for (var attempt = 0; attempt < MaxConcurrencyRetries; attempt++)
        {
            await using var tx = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                var account = await db.CreditAccounts
                    .SingleOrDefaultAsync(x => x.UserId == targetUserId, ct);

                if (account == null)
                    return Result<CreditGrantReceipt>.Failure(Error.NotFound("CreditAccountNotFound", "Tài khoản credit không tồn tại."));

                // Check replay of same idempotency key for this user
                var existingEntry = await db.CreditLedger
                    .SingleOrDefaultAsync(x => x.UserId == targetUserId && x.OperationKey == idempotencyKey, ct);

                if (existingEntry != null)
                {
                    await tx.RollbackAsync(ct);
                    return Result<CreditGrantReceipt>.Success(new CreditGrantReceipt(
                        targetUserId,
                        existingEntry.Delta,
                        account.TrialBalance,
                        account.PurchasedBalance
                    ));
                }

                account.GrantPurchased(amount);

                var ledgerEntry = CreditLedgerEntry.CreateAdminAdjustment(
                    targetUserId,
                    CreditBucket.Purchased,
                    amount,
                    adminUserId,
                    idempotencyKey,
                    reason,
                    clock.UtcNow
                );

                db.CreditLedger.Add(ledgerEntry);

                // Synchronize legacy User fields for temporary caller backward-compatibility
                var user = await db.Users.SingleOrDefaultAsync(x => x.Id == targetUserId, ct);
                if (user != null)
                {
                    user.PurchasedRemaining = account.PurchasedBalance;
                }

                var audit = AuditLog.Create(
                    adminUserId,
                    "Credit.AdminGrant",
                    "CreditAccount",
                    targetUserId,
                    idempotencyKey,
                    System.Text.Json.JsonSerializer.Serialize(new { amount, reason = reason.Trim(), bucket = "Purchased" }),
                    reason.Trim(),
                    clock.UtcNow
                );
                db.AuditLogs.Add(audit);

                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                return Result<CreditGrantReceipt>.Success(new CreditGrantReceipt(
                    targetUserId,
                    amount,
                    account.TrialBalance,
                    account.PurchasedBalance
                ));
            }
            catch (Exception ex) when (attempt < MaxConcurrencyRetries - 1 && IsTransientOrConcurrency(ex))
            {
                await tx.RollbackAsync(ct);
                (db as DbContext)?.ChangeTracker.Clear();
                await Task.Delay(25 * (attempt + 1), ct);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                (db as DbContext)?.ChangeTracker.Clear();
                throw;
            }
        }

        return Result<CreditGrantReceipt>.Failure(Error.Conflict("ConcurrencyConflict", "Hệ thống đang bận xử lý số dư. Vui lòng thử lại."));
    }

    public async Task<CreditBalanceDto> GetBalanceAsync(Guid userId, CancellationToken ct)
    {
        var account = await db.CreditAccounts.AsNoTracking()
            .SingleOrDefaultAsync(x => x.UserId == userId, ct);

        if (account == null) return new CreditBalanceDto(0, 0, 0);

        return new CreditBalanceDto(account.TrialBalance, account.PurchasedBalance, account.TotalBalance);
    }

    private static bool IsTransientOrConcurrency(Exception? ex)
    {
        while (ex != null)
        {
            if (ex is DbUpdateConcurrencyException) return true;
            if (ex is Microsoft.Data.SqlClient.SqlException sql && (sql.Number == 1205 || sql.Number == -2)) return true;
            ex = ex.InnerException;
        }
        return false;
    }
}

