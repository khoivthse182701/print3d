using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TheBoys.Application.Defects.Commands.CreateDefectReport;
using TheBoys.Application.Defects.Commands.ResolveDefectReport;
using TheBoys.Application.Defects.Queries;
using TheBoys.WebApi.Security;

namespace TheBoys.WebApi.Controllers.v2;

public sealed record CreateDefectReportApiRequest(
    Guid? ClientRequestId,
    string Reason,
    List<DefectEvidenceInput>? Evidence = null
);

public sealed record ResolveDefectReportApiRequest(
    string ResolutionType,
    string? ResolutionNote = null
);

[ApiController, Route("api/v2"), Authorize]
public sealed class DefectReportsV2Controller(ISender mediator) : ApiControllerBase
{
    [HttpGet("defect-reports")]
    [Authorize(Roles = Roles.Management)]
    public async Task<IActionResult> GetDefectReports(
        [FromQuery] string? status = null,
        [FromQuery] Guid? orderId = null,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetDefectReportsQuery(status, orderId), ct);
        if (result.IsFailure)
        {
            return ProblemResult(result.Error);
        }

        return Ok(result.Value);
    }

    [HttpPost("orders/{orderId:guid}/defect-reports")]
    public async Task<IActionResult> CreateDefectReport(
        Guid orderId,
        [FromBody] CreateDefectReportApiRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct)
    {
        Guid clientRequestId;
        if (!string.IsNullOrWhiteSpace(idempotencyKey) && Guid.TryParse(idempotencyKey, out var headerGuid))
        {
            clientRequestId = headerGuid;
        }
        else if (request.ClientRequestId.HasValue && request.ClientRequestId.Value != Guid.Empty)
        {
            clientRequestId = request.ClientRequestId.Value;
        }
        else
        {
            return BadRequest(new { message = "Bắt buộc phải có Idempotency-Key (Header) hoặc ClientRequestId (Body) là UUID hợp lệ." });
        }

        var command = new CreateDefectReportCommand(
            OrderId: orderId,
            UserId: UserId,
            ClientRequestId: clientRequestId,
            Reason: request.Reason,
            Evidence: request.Evidence,
            IsManagement: IsManagement
        );

        var result = await mediator.Send(command, ct);
        if (result.IsFailure)
        {
            return ProblemResult(result.Error);
        }

        var (report, isReplay) = result.Value;
        return isReplay ? Ok(report) : StatusCode(StatusCodes.Status201Created, report);
    }

    [HttpPost("defect-reports/{reportId:guid}/resolve")]
    [Authorize(Roles = Roles.Management)]
    public async Task<IActionResult> ResolveDefectReport(
        Guid reportId,
        [FromBody] ResolveDefectReportApiRequest request,
        CancellationToken ct)
    {
        if (!Enum.TryParse<DefectResolutionType>(request.ResolutionType, true, out var resolutionType))
        {
            return BadRequest(new { message = "Loại giải quyết không hợp lệ. Chấp nhận: ReprintApproved, RefundApproved, Rejected." });
        }

        var command = new ResolveDefectReportCommand(
            ReportId: reportId,
            ActorUserId: UserId,
            ResolutionType: resolutionType,
            ResolutionNote: request.ResolutionNote
        );

        var result = await mediator.Send(command, ct);
        if (result.IsFailure)
        {
            return ProblemResult(result.Error);
        }

        return Ok(result.Value);
    }
}

