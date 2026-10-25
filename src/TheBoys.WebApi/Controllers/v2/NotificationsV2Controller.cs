using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TheBoys.Application.Notifications.Commands;
using TheBoys.Application.Notifications.Queries;
using TheBoys.WebApi.Security;

namespace TheBoys.WebApi.Controllers.v2;

public sealed record DispatchOutboxApiRequest(int? BatchSize = 50);

[ApiController, Route("api/v2"), Authorize]
public sealed class NotificationsV2Controller(ISender mediator) : ApiControllerBase
{
    [HttpGet("notifications")]
    public async Task<IActionResult> GetNotifications([FromQuery] bool unreadOnly = false, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetNotificationsQuery(UserId, unreadOnly), ct);
        if (result.IsFailure)
        {
            return ProblemResult(result.Error);
        }

        return Ok(result.Value);
    }

    [HttpPost("notifications/{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct = default)
    {
        var result = await mediator.Send(new MarkNotificationReadCommand(id, UserId, IsManagement), ct);
        if (result.IsFailure)
        {
            return ProblemResult(result.Error);
        }

        return Ok(result.Value);
    }

    [HttpPost("admin/outbox/dispatch")]
    [Authorize(Roles = Roles.Management)]
    public async Task<IActionResult> DispatchOutbox([FromBody] DispatchOutboxApiRequest? request, CancellationToken ct = default)
    {
        var batchSize = request?.BatchSize ?? 50;
        var result = await mediator.Send(new DispatchOutboxCommand(batchSize), ct);
        if (result.IsFailure)
        {
            return ProblemResult(result.Error);
        }

        return Ok(result.Value);
    }
}
