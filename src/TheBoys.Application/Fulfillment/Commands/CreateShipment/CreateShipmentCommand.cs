using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Application.Common.Models;
using TheBoys.Application.Fulfillment.DTOs;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;

namespace TheBoys.Application.Fulfillment.Commands.CreateShipment;

public sealed record CreateShipmentCommand(
    Guid OrderId,
    Guid ActorUserId,
    string Carrier,
    string TrackingNumber,
    decimal? ShippingActualVnd = null
) : IRequest<Result<ShipmentDto>>;

public sealed class CreateShipmentCommandHandler(
    IApplicationDbContext db,
    IClock clock
) : IRequestHandler<CreateShipmentCommand, Result<ShipmentDto>>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task<Result<ShipmentDto>> Handle(CreateShipmentCommand command, CancellationToken ct)
    {
        var order = await db.Orders
            .SingleOrDefaultAsync(o => o.Id == command.OrderId, ct);

        if (order == null)
            return Result<ShipmentDto>.Failure(Error.NotFound("OrderNotFound", "Không tìm thấy đơn hàng."));

        // Gatekeeper: Chỉ đơn ở trạng thái Packaged hoặc DeliveryFailed mới được tạo vận đơn
        if (order.FulfillmentStatus != "Packaged" && order.FulfillmentStatus != "DeliveryFailed")
        {
            return Result<ShipmentDto>.Failure(Error.Conflict(
                "OrderNotPackaged",
                $"Đơn hàng ở trạng thái fulfillment '{order.FulfillmentStatus}' không đủ điều kiện tạo vận đơn."
            ));
        }

        var existingTracking = await db.Shipments
            .AnyAsync(s => s.Carrier == command.Carrier && s.TrackingNumber == command.TrackingNumber, ct);

        if (existingTracking)
        {
            return Result<ShipmentDto>.Failure(Error.Conflict(
                "TrackingNumberExists",
                "Mã vận đơn này đã tồn tại cho đơn vị vận chuyển đã chọn."
            ));
        }

        var now = clock.UtcNow;

        var shipment = Shipment.Create(
            orderId: order.Id,
            carrier: command.Carrier,
            trackingNumber: command.TrackingNumber,
            shippingActualVnd: command.ShippingActualVnd,
            createdAtUtc: now
        );

        db.Shipments.Add(shipment);

        order.TrackingNumber = command.TrackingNumber;

        var initialEvent = ShipmentEvent.Create(
            shipmentId: shipment.Id,
            eventKey: $"SHIP-INIT-{Guid.NewGuid():N}",
            status: "Created",
            note: "Khởi tạo vận đơn giao hàng",
            source: "Staff",
            actorUserId: command.ActorUserId,
            occurredAtUtc: now,
            receivedAtUtc: now
        );
        db.ShipmentEvents.Add(initialEvent);

        var auditLog = AuditLog.Create(
            actorUserId: command.ActorUserId,
            action: "CreateShipment",
            aggregateType: "Shipment",
            aggregateId: shipment.Id,
            traceId: Guid.NewGuid().ToString("N"),
            redactedChangesJson: JsonSerializer.Serialize(new
            {
                orderId = order.Id,
                carrier = command.Carrier,
                trackingNumber = command.TrackingNumber,
                shippingActualVnd = command.ShippingActualVnd
            }, JsonOptions),
            reason: $"Tạo vận đơn {command.TrackingNumber} ({command.Carrier}) cho đơn {order.OrderCode}",
            utcNow: now
        );
        db.AuditLogs.Add(auditLog);

        var outbox = OutboxMessage.Create(
            eventType: "ShipmentCreated",
            aggregateId: order.Id,
            deduplicationKey: $"shipment:created:{shipment.Id}",
            payloadJson: JsonSerializer.Serialize(new
            {
                shipmentId = shipment.Id,
                orderId = order.Id,
                carrier = command.Carrier,
                trackingNumber = command.TrackingNumber
            }, JsonOptions),
            utcNow: now
        );
        db.OutboxMessages.Add(outbox);

        await db.SaveChangesAsync(ct);

        var eventDtos = new List<ShipmentEventDto>
        {
            new(
                initialEvent.Id,
                initialEvent.ShipmentId,
                initialEvent.EventKey,
                initialEvent.Status,
                initialEvent.Note,
                initialEvent.Source,
                initialEvent.ActorUserId,
                initialEvent.OccurredAtUtc,
                initialEvent.ReceivedAtUtc
            )
        };

        return Result<ShipmentDto>.Success(new ShipmentDto(
            shipment.Id,
            shipment.OrderId,
            shipment.Carrier,
            shipment.TrackingNumber,
            shipment.Status.ToString(),
            shipment.ShippingActualVnd,
            shipment.DeliveredAtUtc,
            shipment.DeliveryEvidenceStorageKey,
            shipment.CreatedAtUtc,
            eventDtos
        ));
    }
}

