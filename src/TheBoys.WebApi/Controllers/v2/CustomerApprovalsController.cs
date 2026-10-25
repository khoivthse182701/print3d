using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using TheBoys.Application.Approvals.Commands.CreateCustomerApproval;

namespace TheBoys.WebApi.Controllers.v2;

public sealed class CreateCustomerApprovalRequest
{
    public Guid VersionId { get; set; }
    public object Configuration { get; set; } = default!;
    public string? PolicyVersion { get; set; }
}

[ApiController, Route("api/v2/approvals"), Authorize]
public sealed class CustomerApprovalsController(ISender mediator) : ApiControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateCustomerApprovalRequest request, CancellationToken ct)
    {
        var command = new CreateCustomerApprovalCommand(
            UserId,
            request.VersionId,
            request.Configuration,
            request.PolicyVersion
        );

        var result = await mediator.Send(command, ct);
        return result.IsFailure ? ProblemResult(result.Error) : Ok(result.Value);
    }
}

