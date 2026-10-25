using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TheBoys.Application.Payments.Commands.ReconcileRefund;
using TheBoys.WebApi.Security;

namespace TheBoys.WebApi.Controllers.v2;

public sealed class ReconcileRefundRequest
{
    public string? Note { get; set; }
}

[ApiController]
[Route("api/v2/refunds")]
[Authorize(Roles = Roles.Management)]
public sealed class RefundsController(ISender mediator) : ApiControllerBase
{
    /// <summary>
    /// Staff/Admin đối soát giao dịch hoàn tiền đang ở trạng thái Submitted hoặc NeedsAttention.
    /// Tra cứu trực tiếp với cổng thanh toán để chốt trạng thái Succeeded hoặc Failed an toàn.
    /// </summary>
    [HttpPost("{refundId:guid}/reconcile")]
    public async Task<IActionResult> Reconcile(
        Guid refundId,
        [FromBody] ReconcileRefundRequest? request,
        CancellationToken ct)
    {
        var command = new ReconcileRefundCommand(refundId, UserId, request?.Note);
        var result = await mediator.Send(command, ct);

        if (result.IsFailure)
        {
            return ProblemResult(result.Error);
        }

        return Ok(result.Value);
    }
}

