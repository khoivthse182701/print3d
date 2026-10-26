using System.ComponentModel.DataAnnotations;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TheBoys.Application.Admin.Commands;
using TheBoys.Application.Admin.Queries;
using TheBoys.WebApi.Security;

namespace TheBoys.WebApi.Controllers;

public sealed class RoleRequest { [Required] public string Role { get; set; } = ""; }
public sealed class CreditsRequest
{
    public Guid? UserId { get; set; }
    [Range(1, 100)] public int Amount { get; set; }
    [Required, StringLength(500, MinimumLength = 3)] public string Reason { get; set; } = "";
    public string? IdempotencyKey { get; set; }
}
public sealed class LockUserRequest
{
    [Required, StringLength(500, MinimumLength = 3)] public string Reason { get; set; } = "";
}
public sealed class PackagePriceRequest
{
    [Range(typeof(decimal), "1", "100000000")] public decimal PriceVnd { get; set; }
    public bool IsActive { get; set; } = true;
}

[ApiController, Route("api/admin"), Authorize(Roles = Roles.Admin)]
public sealed class AdminController(ISender mediator) : ApiControllerBase
{
    [HttpGet("users")]
    public async Task<IActionResult> Users([FromQuery, Range(1, 100000)] int page = 1, CancellationToken ct = default)
    {
        var users = await mediator.Send(new GetUsersQuery(page), ct);
        return Ok(users.Select(u => new
        {
            u.Id,
            u.Email,
            u.FullName,
            role = Roles.Name(u.Role),
            accountStatus = u.AccountStatus.ToString(),
            u.FreeTrialRemaining,
            u.PurchasedRemaining
        }));
    }

    [HttpPatch("users/{id:guid}/role")]
    public async Task<IActionResult> Role(Guid id, RoleRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new UpdateUserRoleCommand(UserId, id, request.Role), ct);
        return result.IsFailure
            ? ProblemResult(result.Error)
            : Ok(new { result.Value.Id, role = Roles.Name(result.Value.Role) });
    }

    [HttpPost("users/credits")]
    public async Task<IActionResult> Credits(
        CreditsRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct)
    {
        if (!request.UserId.HasValue || request.UserId.Value == Guid.Empty)
            return BadRequest(new { message = "UserId là bắt buộc trong JSON body." });

        if (string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(new { message = "Lý do cấp credit là bắt buộc." });

        var key = string.IsNullOrWhiteSpace(idempotencyKey) ? request.IdempotencyKey : idempotencyKey;
        if (string.IsNullOrWhiteSpace(key))
            return BadRequest(new { message = "Idempotency-Key là bắt buộc khi cấp credit." });

        var result = await mediator.Send(new AddUserCreditsCommand(UserId, request.UserId.Value, request.Amount, request.Reason, key), ct);
        return result.IsFailure
            ? ProblemResult(result.Error)
            : Ok(new
            {
                result.Value.UserId,
                amountGranted = result.Value.Amount,
                result.Value.RemainingTrial,
                result.Value.RemainingPurchased
            });
    }

    [HttpPost("users/{id:guid}/credits"), ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> CreditsViaPath(
        Guid id,
        CreditsRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct)
    {
        request.UserId = id;
        return await Credits(request, idempotencyKey, ct);
    }

    [HttpPost("users/{id:guid}/lock")]
    public async Task<IActionResult> LockUser(Guid id, LockUserRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(new { message = "Lý do khóa tài khoản là bắt buộc." });

        var result = await mediator.Send(new LockUserAccountCommand(UserId, id, request.Reason), ct);
        return result.IsFailure
            ? ProblemResult(result.Error)
            : Ok(new { message = "Đã khóa tài khoản thành công.", targetUserId = id });
    }

    [HttpPost("users/{id:guid}/unlock")]
    public async Task<IActionResult> UnlockUser(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new UnlockUserAccountCommand(UserId, id), ct);
        return result.IsFailure
            ? ProblemResult(result.Error)
            : Ok(new { message = "Đã mở khóa tài khoản thành công.", targetUserId = id });
    }

    [HttpGet("pricing-packages")]
    public async Task<IActionResult> GetAllPackages(CancellationToken ct)
    {
        var packages = await mediator.Send(new GetActivePricingPackagesQuery(true), ct);
        return Ok(packages.Select(x => new { x.Id, x.Name, x.Material, x.PriceVnd, x.MinHeightMm, x.MaxHeightMm, x.HeightRangeCm, x.IsActive }));
    }

    [HttpPatch("pricing-packages/{id:guid}")]
    public async Task<IActionResult> UpdatePrice(Guid id, PackagePriceRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new UpdatePricingPackageCommand(id, request.PriceVnd, request.IsActive), ct);
        return result.IsFailure
            ? ProblemResult(result.Error)
            : Ok(new { result.Value.Id, result.Value.PriceVnd, result.Value.IsActive, result.Value.MinHeightMm, result.Value.MaxHeightMm });
    }
}

[ApiController, Route("api/pricing-packages")]
public sealed class PricingPackagesController(ISender mediator) : ControllerBase
{
    [HttpGet, AllowAnonymous]
    public async Task<IActionResult> Get([FromQuery] bool includeInactive = false, CancellationToken ct = default)
    {
        var packages = await mediator.Send(new GetActivePricingPackagesQuery(includeInactive), ct);
        return Ok(packages.Select(x => new { x.Id, x.Name, x.Material, x.PriceVnd, x.MinHeightMm, x.MaxHeightMm, x.HeightRangeCm, x.IsActive }));
    }
}
