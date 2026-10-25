using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TheBoys.Application.Payments.Commands.ProcessPaymentWebhook;

namespace TheBoys.WebApi.Controllers.v2;

[ApiController]
[Route("api/v2/payments")]
public sealed class PaymentWebhookController(ISender mediator) : ApiControllerBase
{
    /// <summary>
    /// Gateway-only webhook ingress point. Validates raw body signatures and idempotently ingests payment events.
    /// Never callable by customer frontends to confirm payment.
    /// </summary>
    [HttpPost("{gateway}/webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> HandleWebhook(string gateway, CancellationToken ct)
    {
        // 1. Giới hạn body size tối đa 64 KB để phòng ngừa DoS
        if (Request.ContentLength.HasValue && Request.ContentLength.Value > 65536)
        {
            return BadRequest(new { message = "Kích thước webhook payload vượt quá giới hạn 64KB." });
        }

        using var ms = new MemoryStream();
        await Request.Body.CopyToAsync(ms, ct);
        var rawBody = ms.ToArray();

        if (rawBody.Length == 0)
        {
            return BadRequest(new { message = "Webhook payload rỗng." });
        }

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var h in Request.Headers)
        {
            headers[h.Key] = h.Value.ToString();
        }

        var command = new ProcessPaymentWebhookCommand(gateway, rawBody, headers);
        var result = await mediator.Send(command, ct);

        if (result.IsFailure)
        {
            return ProblemResult(result.Error);
        }

        return Ok(new
        {
            success = true,
            acknowledged = result.Value.Acknowledged,
            message = result.Value.Message,
            processingResult = result.Value.ProcessingResult
        });
    }
}

