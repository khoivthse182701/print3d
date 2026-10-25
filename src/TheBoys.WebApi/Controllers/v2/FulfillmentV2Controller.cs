using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TheBoys.Application.Fulfillment.Commands.AssignOrderToPartner;
using TheBoys.Application.Fulfillment.Commands.CreateShipment;
using TheBoys.Application.Fulfillment.Commands.RecordProductionRunQc;
using TheBoys.Application.Fulfillment.Commands.RecordShipmentEvent;
using TheBoys.Application.Fulfillment.Commands.RespondPartnerAssignment;
using TheBoys.Application.Fulfillment.Commands.StartProductionRun;
using TheBoys.Application.Fulfillment.Commands.UpdateProductionRunProgress;
using TheBoys.Application.Fulfillment.Queries.GetOrderFulfillmentTimeline;
using TheBoys.Domain.Enums;
using TheBoys.WebApi.Security;
using Microsoft.EntityFrameworkCore;
using TheBoys.Application.Common.Interfaces;

namespace TheBoys.WebApi.Controllers.v2;

public sealed record AssignOrderApiRequest(
    Guid PartnerLabId,
    decimal? PartnerCostSnapshotVnd = null,
    DateTime? DueAtUtc = null,
    string? DecisionNote = null
);

public sealed record RespondAssignmentApiRequest(
    bool Accepted,
    string? DecisionNote = null
);

public sealed record StartProductionRunApiRequest(
    int? Quantity = null,
    decimal? ActualCostVnd = null
);

public sealed record UpdateProductionRunProgressApiRequest(
    string Status
);

public sealed record RecordProductionRunQcApiRequest(
    bool Passed,
    string? QcEvidenceStorageKey = null,
    string? QcNote = null,
    bool TriggerReprint = false,
    decimal? ActualCostVnd = null
);

public sealed record CreateShipmentApiRequest(
    string Carrier,
    string TrackingNumber,
    decimal? ShippingActualVnd = null
);

public sealed record RecordShipmentEventApiRequest(
    string EventKey,
    string Status,
    string? Note = null,
    string? Source = null,
    DateTime? OccurredAtUtc = null,
    string? DeliveryEvidenceStorageKey = null
);

[ApiController, Route("api/v2"), Authorize]
public sealed class FulfillmentV2Controller(ISender mediator) : ApiControllerBase
{
    [HttpGet("partner-labs")]
    [Authorize(Roles = Roles.Management)]
    public async Task<IActionResult> GetPartnerLabs(
        [FromServices] IApplicationDbContext db,
        CancellationToken ct)
    {
        var labs = await db.PartnerLabs.AsNoTracking()
            .Where(l => l.IsActive)
            .OrderBy(l => l.LabName)
            .Select(l => new
            {
                id = l.Id,
                name = l.LabName,
                contactPhone = l.PhoneNumber,
                address = l.Address,
                isActive = l.IsActive
            })
            .ToListAsync(ct);

        return Ok(labs);
    }
    [HttpPost("orders/{orderId:guid}/assignments")]
    [Authorize(Roles = Roles.Management)]
    public async Task<IActionResult> AssignOrder(
        Guid orderId,
        [FromBody] AssignOrderApiRequest request,
        CancellationToken ct)
    {
        var command = new AssignOrderToPartnerCommand(
            OrderId: orderId,
            PartnerLabId: request.PartnerLabId,
            AssignedByUserId: UserId,
            PartnerCostSnapshotVnd: request.PartnerCostSnapshotVnd,
            DueAtUtc: request.DueAtUtc,
            DecisionNote: request.DecisionNote
        );

        var result = await mediator.Send(command, ct);
        if (result.IsFailure)
        {
            return ProblemResult(result.Error);
        }

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPost("assignments/{assignmentId:guid}/respond")]
    [Authorize(Roles = Roles.Management)]
    public async Task<IActionResult> RespondAssignment(
        Guid assignmentId,
        [FromBody] RespondAssignmentApiRequest request,
        CancellationToken ct)
    {
        var command = new RespondPartnerAssignmentCommand(
            AssignmentId: assignmentId,
            ActorUserId: UserId,
            Accepted: request.Accepted,
            DecisionNote: request.DecisionNote,
            IsManagement: IsManagement
        );

        var result = await mediator.Send(command, ct);
        if (result.IsFailure)
        {
            return ProblemResult(result.Error);
        }

        return Ok(result.Value);
    }

    [HttpPost("assignments/{assignmentId:guid}/production-runs")]
    [Authorize(Roles = Roles.Management)]
    public async Task<IActionResult> StartProductionRun(
        Guid assignmentId,
        [FromBody] StartProductionRunApiRequest? request,
        CancellationToken ct)
    {
        var command = new StartProductionRunCommand(
            AssignmentId: assignmentId,
            ActorUserId: UserId,
            Quantity: request?.Quantity,
            ActualCostVnd: request?.ActualCostVnd
        );

        var result = await mediator.Send(command, ct);
        if (result.IsFailure)
        {
            return ProblemResult(result.Error);
        }

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPost("production-runs/{runId:guid}/status")]
    [Authorize(Roles = Roles.Management)]
    public async Task<IActionResult> UpdateRunStatus(
        Guid runId,
        [FromBody] UpdateProductionRunProgressApiRequest request,
        CancellationToken ct)
    {
        if (!Enum.TryParse<ProductionRunStatus>(request.Status, true, out var status))
        {
            return BadRequest(new { message = "Trạng thái không hợp lệ. Chỉ chấp nhận PostProcessing hoặc QualityCheck." });
        }

        var command = new UpdateProductionRunProgressCommand(
            RunId: runId,
            ActorUserId: UserId,
            NewStatus: status
        );

        var result = await mediator.Send(command, ct);
        if (result.IsFailure)
        {
            return ProblemResult(result.Error);
        }

        return Ok(result.Value);
    }

    [HttpPost("production-runs/{runId:guid}/qc")]
    [Authorize(Roles = Roles.Management)]
    public async Task<IActionResult> RecordQc(
        Guid runId,
        [FromBody] RecordProductionRunQcApiRequest request,
        CancellationToken ct)
    {
        var command = new RecordProductionRunQcCommand(
            RunId: runId,
            ActorUserId: UserId,
            Passed: request.Passed,
            QcEvidenceStorageKey: request.QcEvidenceStorageKey,
            QcNote: request.QcNote,
            TriggerReprint: request.TriggerReprint,
            ActualCostVnd: request.ActualCostVnd
        );

        var result = await mediator.Send(command, ct);
        if (result.IsFailure)
        {
            return ProblemResult(result.Error);
        }

        return Ok(result.Value);
    }

    [HttpPost("orders/{orderId:guid}/shipments")]
    [Authorize(Roles = Roles.Management)]
    public async Task<IActionResult> CreateShipment(
        Guid orderId,
        [FromBody] CreateShipmentApiRequest request,
        CancellationToken ct)
    {
        var command = new CreateShipmentCommand(
            OrderId: orderId,
            ActorUserId: UserId,
            Carrier: request.Carrier,
            TrackingNumber: request.TrackingNumber,
            ShippingActualVnd: request.ShippingActualVnd
        );

        var result = await mediator.Send(command, ct);
        if (result.IsFailure)
        {
            return ProblemResult(result.Error);
        }

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPost("shipments/{shipmentId:guid}/events")]
    [Authorize(Roles = Roles.Management)]
    public async Task<IActionResult> RecordShipmentEvent(
        Guid shipmentId,
        [FromBody] RecordShipmentEventApiRequest request,
        CancellationToken ct)
    {
        var command = new RecordShipmentEventCommand(
            ShipmentId: shipmentId,
            EventKey: request.EventKey,
            Status: request.Status,
            Note: request.Note,
            Source: request.Source ?? "Staff",
            ActorUserId: UserId,
            OccurredAtUtc: request.OccurredAtUtc,
            DeliveryEvidenceStorageKey: request.DeliveryEvidenceStorageKey
        );

        var result = await mediator.Send(command, ct);
        if (result.IsFailure)
        {
            return ProblemResult(result.Error);
        }

        var (shipment, isReplay) = result.Value;
        return isReplay ? Ok(shipment) : StatusCode(StatusCodes.Status201Created, shipment);
    }

    [HttpGet("orders/{orderId:guid}/fulfillment")]
    [HttpGet("orders/{orderId:guid}/timeline")]
    public async Task<IActionResult> GetOrderFulfillment(
        Guid orderId,
        CancellationToken ct)
    {
        var query = new GetOrderFulfillmentTimelineQuery(orderId, UserId, IsManagement);
        var result = await mediator.Send(query, ct);

        if (result.IsFailure)
        {
            return ProblemResult(result.Error);
        }

        return Ok(result.Value);
    }
}

