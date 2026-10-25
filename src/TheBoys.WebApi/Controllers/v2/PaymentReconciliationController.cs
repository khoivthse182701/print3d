using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TheBoys.Application.Payments.Commands.ReconcilePaymentAttempt;
using TheBoys.WebApi.Security;

namespace TheBoys.WebApi.Controllers.v2;

[ApiController]
[Route("api/v2/payments")]
[Authorize(Roles = Roles.Management)]
public sealed class PaymentReconciliationController(ISender mediator) : ApiControllerBase
{
    /// <summary>
    /// Staff/Admin reconciliation endpoint for NeedsAttention or uncertain payment attempts.
    /// Reconciles against the gateway using the existing MerchantReference without generating new charges.
    /// </summary>
    [HttpPost("reconcile/{attemptId:guid}")]
    public async Task<IActionResult> Reconcile(Guid attemptId, CancellationToken ct)
    {
        var command = new ReconcilePaymentAttemptCommand(attemptId, UserId);
        var result = await mediator.Send(command, ct);

        if (result.IsFailure)
        {
            return ProblemResult(result.Error);
        }

        return Ok(result.Value);
    }
}
