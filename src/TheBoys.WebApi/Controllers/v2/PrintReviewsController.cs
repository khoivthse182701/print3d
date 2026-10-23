using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using TheBoys.Application.Reviews;
using TheBoys.Application.Reviews.Commands.CreatePrintReview;
using TheBoys.WebApi.Security;

namespace TheBoys.WebApi.Controllers.v2;

public sealed class CreatePrintReviewRequest
{
    public Guid VersionId { get; set; }
    public string Decision { get; set; } = "Approved";
    public PrintReviewChecklistDto Checklist { get; set; } = new();
    public object Configuration { get; set; } = default!;
    public string? EvidenceStorageKey { get; set; }
    public string Note { get; set; } = default!;
}

[ApiController, Route("api/v2/reviews"), Authorize(Roles = Roles.Management)]
public sealed class PrintReviewsController(ISender mediator) : ApiControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreatePrintReviewRequest request, CancellationToken ct)
    {
        var command = new CreatePrintReviewCommand(
            request.VersionId,
            UserId,
            request.Decision,
            request.Checklist,
            request.Configuration,
            request.EvidenceStorageKey,
            request.Note
        );

        var result = await mediator.Send(command, ct);
        return result.IsFailure ? ProblemResult(result.Error) : Ok(result.Value);
    }
}

