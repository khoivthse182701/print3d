using System.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Application.Common.Models;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;

namespace TheBoys.Application.Admin.Commands;

public sealed record UpdateUserRoleCommand(
    Guid CurrentAdminUserId,
    Guid TargetUserId,
    string Role
) : IRequest<Result<User>>;

public sealed class UpdateUserRoleCommandHandler(
    IApplicationDbContext db,
    IUserRoleCache userRoleCache) : IRequestHandler<UpdateUserRoleCommand, Result<User>>
{
    public async Task<Result<User>> Handle(UpdateUserRoleCommand request, CancellationToken ct)
    {
        if (request.TargetUserId == request.CurrentAdminUserId)
            return Result<User>.Failure(Error.Conflict("CannotChangeOwnRole", "Không đổi role của chính tài khoản Admin đang sử dụng."));

        var role = request.Role.ToLowerInvariant() switch
        {
            "cus" or "customer" => UserRole.Customer,
            "staff" => UserRole.Staff,
            "manager" => UserRole.Manager,
            "admin" => UserRole.Admin,
            _ => (UserRole?)null
        };

        if (role == null)
            return Result<User>.Failure(Error.Validation("InvalidRole", "Role phải là cus, staff, manager hoặc admin."));

        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == request.TargetUserId, ct);
        if (user == null) return Result<User>.Failure(Error.NotFound("UserNotFound"));

        if (user.Role == UserRole.Admin && role != UserRole.Admin && await db.Users.CountAsync(x => x.Role == UserRole.Admin, ct) <= 1)
            return Result<User>.Failure(Error.Conflict("LastAdmin", "Phải giữ ít nhất một Admin."));

        user.Role = role.Value;
        user.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        // Immediately evict cached role so subsequent requests use the new role
        userRoleCache.Remove(request.TargetUserId);

        return Result<User>.Success(user);
    }
}

public sealed record AddUserCreditsCommand(
    Guid AdminUserId,
    Guid TargetUserId,
    int Amount,
    string Reason,
    string IdempotencyKey
) : IRequest<Result<CreditGrantReceipt>>;

public sealed class AddUserCreditsCommandHandler(ICreditService creditService)
    : IRequestHandler<AddUserCreditsCommand, Result<CreditGrantReceipt>>
{
    public async Task<Result<CreditGrantReceipt>> Handle(AddUserCreditsCommand request, CancellationToken ct)
    {
        return await creditService.AdminGrantAsync(
            request.TargetUserId,
            request.AdminUserId,
            request.Amount,
            request.Reason,
            request.IdempotencyKey,
            ct);
    }
}

public sealed record LockUserAccountCommand(
    Guid AdminUserId,
    Guid TargetUserId,
    string Reason
) : IRequest<Result>;

public sealed class LockUserAccountCommandHandler(
    IApplicationDbContext db,
    IAuditService auditService,
    IClock clock,
    IUserRoleCache userRoleCache) : IRequestHandler<LockUserAccountCommand, Result>
{
    public async Task<Result> Handle(LockUserAccountCommand request, CancellationToken ct)
    {
        if (request.TargetUserId == request.AdminUserId)
            return Result.Failure(Error.Conflict("CannotLockSelf", "Không thể tự khóa tài khoản của chính mình."));

        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == request.TargetUserId, ct);
        if (user == null) return Result.Failure(Error.NotFound("UserNotFound"));

        if (user.Role == UserRole.Admin && await db.Users.CountAsync(x => x.Role == UserRole.Admin && x.AccountStatus != AccountStatus.Locked, ct) <= 1)
            return Result.Failure(Error.Conflict("LastAdmin", "Không thể khóa tài khoản Admin duy nhất còn lại."));

        var now = clock.UtcNow;
        user.Lock(now);

        var sessions = await db.RefreshSessions.Where(s => s.UserId == request.TargetUserId && s.RevokedAtUtc == null).ToListAsync(ct);
        foreach (var s in sessions)
        {
            s.Revoke(now);
        }

        await auditService.RecordAsync(
            request.AdminUserId,
            "UserAccountLocked",
            "User",
            request.TargetUserId,
            Guid.NewGuid().ToString("N"),
            new { reason = request.Reason },
            request.Reason,
            ct);

        await db.SaveChangesAsync(ct);
        userRoleCache.Remove(request.TargetUserId);

        return Result.Success();
    }
}

public sealed record UnlockUserAccountCommand(
    Guid AdminUserId,
    Guid TargetUserId
) : IRequest<Result>;

public sealed class UnlockUserAccountCommandHandler(
    IApplicationDbContext db,
    IAuditService auditService,
    IUserRoleCache userRoleCache) : IRequestHandler<UnlockUserAccountCommand, Result>
{
    public async Task<Result> Handle(UnlockUserAccountCommand request, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == request.TargetUserId, ct);
        if (user == null) return Result.Failure(Error.NotFound("UserNotFound"));

        user.Unlock();

        await auditService.RecordAsync(
            request.AdminUserId,
            "UserAccountUnlocked",
            "User",
            request.TargetUserId,
            Guid.NewGuid().ToString("N"),
            new { },
            "Account unlocked by admin",
            ct);

        await db.SaveChangesAsync(ct);
        userRoleCache.Remove(request.TargetUserId);

        return Result.Success();
    }
}

public sealed record UpdatePricingPackageCommand(
    Guid PackageId,
    decimal PriceVnd,
    bool IsActive
) : IRequest<Result<PricingPackage>>;

public sealed class UpdatePricingPackageCommandHandler(IApplicationDbContext db)
    : IRequestHandler<UpdatePricingPackageCommand, Result<PricingPackage>>
{
    public async Task<Result<PricingPackage>> Handle(UpdatePricingPackageCommand request, CancellationToken ct)
    {
        var package = await db.PricingPackages.SingleOrDefaultAsync(x => x.Id == request.PackageId, ct);
        if (package == null) return Result<PricingPackage>.Failure(Error.NotFound("PackageNotFound"));

        package.PriceVnd = request.PriceVnd;
        package.IsActive = request.IsActive;

        await db.SaveChangesAsync(ct);
        return Result<PricingPackage>.Success(package);
    }
}
