using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Application.Common.Models;
using TheBoys.Application.Fulfillment.DTOs;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;

namespace TheBoys.Application.Fulfillment.Commands.RecordShipmentEvent;

public sealed record RecordShipmentEventResult(ShipmentDto Shipment, bool IsReplay);

public sealed record RecordShipmentEventCommand(
    Guid ShipmentId,
    string EventKey,
    string Status,
    string? Note = null,
    string Source = "Staff",
    Guid? ActorUserId = null,
    DateTime? OccurredAtUtc = null,
    string? DeliveryEvidenceStorageKey = null
) : IRequest<Result<RecordShipmentEventResult>>;

public sealed class RecordShipmentEventCommandHandler(
    IApplicationDbContext db,
    IClock clock
) : IRequestHandler<RecordShipmentEventCommand, Result<RecordShipmentEventResult>>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task<Result<RecordShipmentEventResult>> Handle(RecordShipmentEventCommand command, CancellationToken ct)
    {
        var shipment = await db.Shipments
            .Include(s => s.Order)
            .Include(s => s.Events)
            .SingleOrDefaultAsync(s => s.Id == command.ShipmentId, ct);

        if (shipment == null)
            return Result<RecordShipmentEventResult>.Failure(Error.NotFound("ShipmentNotFound", "Không tìm thấy vận đơn."));

        // Idempotency: Kiểm tra nếu EventKey đã tồn tại trên ShipmentId
        var existingEvent = shipment.Events
            .FirstOrDefault(e => string.Equals(e.EventKey, command.EventKey, StringComparison.OrdinalIgnoreCase));

        if (existingEvent != null)
        {
            return Result<RecordShipmentEventResult>.Success(new RecordShipmentEventResult(MapToDto(shipment), IsReplay: true));
        }

        var now = clock.UtcNow;
        var eventTime = command.OccurredAtUtc ?? now;
        var order = shipment.Order;
        var prevFulfillment = order.FulfillmentStatus;

        if (string.Equals(command.Status, "Shipping", StringComparison.OrdinalIgnoreCase))
        {
            shipment.MarkShipping();
            order.FulfillmentStatus = "Shipping";
            order.ShippedAtUtc ??= eventTime;

            var history = OrderStatusHistory.Create(
                orderId: order.Id,
                axis: "Fulfillment",
                fromStatus: prevFulfillment,
                toStatus: "Shipping",
                actorUserId: command.ActorUserId,
                source: command.Source,
                reason: $"Đang vận chuyển (Đơn vị: {shipment.Carrier}, Mã: {shipment.TrackingNumber}): {command.Note ?? "Rời bưu cục"}",
                operationKey: $"shipment:shipping:{shipment.Id}:{command.EventKey}",
                createdAtUtc: now
            );
            db.OrderStatusHistories.Add(history);
        }
        else if (string.Equals(command.Status, "Delivered", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(command.DeliveryEvidenceStorageKey))
            {
                return Result<RecordShipmentEventResult>.Failure(Error.Conflict(
                    "DeliveryEvidenceRequired",
                    "Xác nhận giao hàng thành công bắt buộc phải có ảnh/bằng chứng ký nhận (DeliveryEvidenceStorageKey)."
                ));
            }

            shipment.MarkDelivered(eventTime, command.DeliveryEvidenceStorageKey);
            order.FulfillmentStatus = "Delivered";
            order.DeliveredAtUtc = eventTime;

            var history = OrderStatusHistory.Create(
                orderId: order.Id,
                axis: "Fulfillment",
                fromStatus: prevFulfillment,
                toStatus: "Delivered",
                actorUserId: command.ActorUserId,
                source: command.Source,
                reason: $"Giao hàng thành công: {command.Note ?? "Khách đã ký nhận"}",
                operationKey: $"shipment:delivered:{shipment.Id}:{command.EventKey}",
                createdAtUtc: now
            );
            db.OrderStatusHistories.Add(history);

            var outbox = OutboxMessage.Create(
                eventType: "OrderDelivered",
                aggregateId: order.Id,
                deduplicationKey: $"order:delivered:{shipment.Id}",
                payloadJson: JsonSerializer.Serialize(new
                {
                    orderId = order.Id,
                    shipmentId = shipment.Id,
                    carrier = shipment.Carrier,
                    trackingNumber = shipment.TrackingNumber,
                    deliveredAtUtc = eventTime
                }, JsonOptions),
                utcNow: now
            );
            db.OutboxMessages.Add(outbox);
        }
        else if (string.Equals(command.Status, "DeliveryFailed", StringComparison.OrdinalIgnoreCase))
        {
            shipment.MarkDeliveryFailed();
            order.FulfillmentStatus = "DeliveryFailed";

            var history = OrderStatusHistory.Create(
                orderId: order.Id,
                axis: "Fulfillment",
                fromStatus: prevFulfillment,
                toStatus: "DeliveryFailed",
                actorUserId: command.ActorUserId,
                source: command.Source,
                reason: $"Giao hàng thất bại: {command.Note ?? "Không liên lạc được người nhận"}",
                operationKey: $"shipment:failed:{shipment.Id}:{command.EventKey}",
                createdAtUtc: now
            );
            db.OrderStatusHistories.Add(history);
        }
        else if (string.Equals(command.Status, "Returned", StringComparison.OrdinalIgnoreCase))
        {
            shipment.MarkReturned();
            order.FulfillmentStatus = "Returned";

            var history = OrderStatusHistory.Create(
                orderId: order.Id,
                axis: "Fulfillment",
                fromStatus: prevFulfillment,
                toStatus: "Returned",
                actorUserId: command.ActorUserId,
                source: command.Source,
                reason: $"Hàng bị hoàn trả về kho: {command.Note ?? "Quá số lần giao"}",
                operationKey: $"shipment:returned:{shipment.Id}:{command.EventKey}",
                createdAtUtc: now
            );
            db.OrderStatusHistories.Add(history);
        }

        var shipmentEvent = ShipmentEvent.Create(
            shipmentId: shipment.Id,
            eventKey: command.EventKey,
            status: command.Status,
            note: command.Note,
            source: command.Source,
            actorUserId: command.ActorUserId,
            occurredAtUtc: eventTime,
            receivedAtUtc: now
        );
        db.ShipmentEvents.Add(shipmentEvent);

        var auditLog = AuditLog.Create(
            actorUserId: command.ActorUserId,
            action: "RecordShipmentEvent",
            aggregateType: "Shipment",
            aggregateId: shipment.Id,
            traceId: Guid.NewGuid().ToString("N"),
            redactedChangesJson: JsonSerializer.Serialize(new
            {
                shipmentId = shipment.Id,
                eventKey = command.EventKey,
                status = command.Status,
                note = command.Note
            }, JsonOptions),
            reason: $"Cập nhật sự kiện vận chuyển {command.Status} cho mã {shipment.TrackingNumber}",
            utcNow: now
        );
        db.AuditLogs.Add(auditLog);

        await db.SaveChangesAsync(ct);

        shipment.Events.Add(shipmentEvent);
        return Result<RecordShipmentEventResult>.Success(new RecordShipmentEventResult(MapToDto(shipment), IsReplay: false));
    }

    private static ShipmentDto MapToDto(Shipment s) => new(
        s.Id,
        s.OrderId,
        s.Carrier,
        s.TrackingNumber,
        s.Status.ToString(),
        s.ShippingActualVnd,
        s.DeliveredAtUtc,
        s.DeliveryEvidenceStorageKey,
        s.CreatedAtUtc,
        s.Events.OrderBy(e => e.OccurredAtUtc).Select(e => new ShipmentEventDto(
            e.Id,
            e.ShipmentId,
            e.EventKey,
            e.Status,
            e.Note,
            e.Source,
            e.ActorUserId,
            e.OccurredAtUtc,
            e.ReceivedAtUtc
        )).ToList()
    );
}

