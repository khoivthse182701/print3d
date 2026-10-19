using System.ComponentModel.DataAnnotations;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TheBoys.Application.Admin.Commands;
using TheBoys.Application.Admin.Queries;
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
public sealed class AdminController(ISender mediator) : ApiControllerBase
{
    [HttpGet("users")]
    public async Task<IActionResult> Users([FromQuery, Range(1, 100000)] int page = 1, CancellationToken ct = default)
    {
        var users = await mediator.Send(new GetUsersQuery(page), ct);
        return Ok(users.Select(u => new { u.Id, u.Email, u.FullName, role = Roles.Name(u.Role), u.FreeTrialRemaining, u.PurchasedRemaining }));
    }

    [HttpPatch("users/{id:guid}/role")]
    public async Task<IActionResult> Role(Guid id, RoleRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new UpdateUserRoleCommand(UserId, id, request.Role), ct);
        return result.IsFailure
            ? ProblemResult(result.Error)
            : Ok(new { result.Value.Id, role = Roles.Name(result.Value.Role) });
    }

    [HttpPost("users/{id:guid}/credits")]
    public async Task<IActionResult> Credits(Guid id, CreditsRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new AddUserCreditsCommand(id, request.Amount), ct);
        return result.IsFailure
            ? ProblemResult(result.Error)
            : Ok(new { result.Value.Id, result.Value.FreeTrialRemaining, result.Value.PurchasedRemaining });
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
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var packages = await mediator.Send(new GetActivePricingPackagesQuery(), ct);
        return Ok(packages.Select(x => new { x.Id, x.Name, x.Material, x.PriceVnd, x.MinHeightMm, x.MaxHeightMm, x.HeightRangeCm }));
    }
}
