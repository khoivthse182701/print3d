using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TheBoys.Domain.Enums;
using TheBoys.Infrastructure.Persistence;
using TheBoys.WebApi.Security;

namespace TheBoys.WebApi.Controllers;

public sealed class RoleRequest { [Required] public string Role { get; set; } = ""; }
public sealed class CreditsRequest { [Range(1, 100)] public int Amount { get; set; } }
public sealed class PackagePriceRequest
{
    [Range(typeof(decimal), "1", "100000000")] public decimal PriceVnd { get; set; }
    public bool IsActive { get; set; } = true;
}

[ApiController, Route("api/admin"), Authorize(Roles = Roles.Admin)]
public sealed class AdminController(ApplicationDbContext db) : ApiControllerBase
{
    [HttpGet("users")]
    public async Task<IActionResult> Users([FromQuery, Range(1, 100000)] int page = 1, CancellationToken ct = default)
    {
        var users = await db.Users.AsNoTracking().OrderBy(x => x.CreatedAtUtc).Skip((page - 1) * 50).Take(50).ToListAsync(ct);
        return Ok(users.Select(u => new { u.Id, u.Email, u.FullName, role = Roles.Name(u.Role), u.FreeTrialRemaining, u.PurchasedRemaining }));
    }

    [HttpPatch("users/{id:guid}/role")]
    public async Task<IActionResult> Role(Guid id, RoleRequest request, CancellationToken ct)
    {
        if (id == UserId) return Conflict(new { message = "Không đổi role của chính tài khoản Admin đang sử dụng." });
        var role = request.Role.ToLowerInvariant() switch { "cus" or "customer" => UserRole.Customer, "staff" => UserRole.Staff, "admin" => UserRole.Admin, _ => (UserRole?)null };
        if (role == null) return BadRequest(new { message = "Role phải là cus, staff hoặc admin." });
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (user == null) return NotFound();
        if (user.Role == UserRole.Admin && role != UserRole.Admin && await db.Users.CountAsync(x => x.Role == UserRole.Admin, ct) <= 1)
            return Conflict(new { message = "Phải giữ ít nhất một Admin." });
        user.Role = role.Value; user.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Ok(new { user.Id, role = Roles.Name(user.Role) });
    }

    [HttpPost("users/{id:guid}/credits")]
    public async Task<IActionResult> Credits(Guid id, CreditsRequest request, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (user == null) return NotFound();
        user.PurchasedRemaining += request.Amount; user.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(new { user.Id, user.FreeTrialRemaining, user.PurchasedRemaining });
    }

    [HttpPatch("pricing-packages/{id:guid}")]
    public async Task<IActionResult> UpdatePrice(Guid id, PackagePriceRequest request, CancellationToken ct)
    {
        var package = await db.PricingPackages.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (package == null) return NotFound();
        package.PriceVnd = request.PriceVnd; package.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);
        return Ok(new { package.Id, package.PriceVnd, package.IsActive, package.MinHeightMm, package.MaxHeightMm });
    }
}

[ApiController, Route("api/pricing-packages")]
public sealed class PricingPackagesController(ApplicationDbContext db) : ControllerBase
{
    [HttpGet, AllowAnonymous]
    public async Task<IActionResult> Get(CancellationToken ct) => Ok(await db.PricingPackages.AsNoTracking()
        .Where(x => x.IsActive).Select(x => new { x.Id, x.Name, x.Material, x.PriceVnd, x.MinHeightMm, x.MaxHeightMm, x.HeightRangeCm }).ToListAsync(ct));
}
