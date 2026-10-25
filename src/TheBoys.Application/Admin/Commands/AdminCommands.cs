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
            "admin" => UserRole.Admin,
            "manager" or "mgr" => UserRole.Manager,
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
    Guid TargetUserId,
    int Amount
) : IRequest<Result<User>>;

public sealed class AddUserCreditsCommandHandler(IApplicationDbContext db)
    : IRequestHandler<AddUserCreditsCommand, Result<User>>
{
    public async Task<Result<User>> Handle(AddUserCreditsCommand request, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == request.TargetUserId, ct);
        if (user == null) return Result<User>.Failure(Error.NotFound("UserNotFound"));

        user.PurchasedRemaining += request.Amount;
        user.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return Result<User>.Success(user);
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
