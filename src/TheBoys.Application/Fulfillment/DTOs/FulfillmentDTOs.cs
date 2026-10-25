using TheBoys.Application.Defects.DTOs;

namespace TheBoys.Application.Fulfillment.DTOs;

public sealed record ProductionRunDto(
    Guid Id,
    Guid AssignmentId,
    int RunNo,
    string Status,
    int Quantity,
    decimal? ActualCostVnd,
    string? QcEvidenceStorageKey,
    string? QcNote,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc
);

public sealed record PartnerAssignmentDto(
    Guid Id,
    Guid OrderId,
    Guid PartnerLabId,
    string? LabName,
    Guid AssignedByUserId,
    string Status,
    bool IsActive,
    decimal PartnerCostSnapshotVnd,
    DateTime DueAtUtc,
    string? DecisionNote,
    DateTime CreatedAtUtc,
    List<ProductionRunDto> ProductionRuns
);

public sealed record ShipmentEventDto(
    Guid Id,
    Guid ShipmentId,
    string EventKey,
    string Status,
    string? Note,
    string Source,
    Guid? ActorUserId,
    DateTime OccurredAtUtc,
    DateTime ReceivedAtUtc
);

public sealed record ShipmentDto(
    Guid Id,
    Guid OrderId,
    string Carrier,
    string TrackingNumber,
    string Status,
    decimal? ShippingActualVnd,
    DateTime? DeliveredAtUtc,
    string? DeliveryEvidenceStorageKey,
    DateTime CreatedAtUtc,
    List<ShipmentEventDto> Events
);

public sealed record OrderFulfillmentTimelineDto(
    Guid OrderId,
    string OrderCode,
    string FulfillmentStatus,
    string OrderPaymentStatus,
    Guid? PartnerLabId,
    string? TrackingNumber,
    List<PartnerAssignmentDto> Assignments,
    List<ShipmentDto> Shipments,
    List<DefectReportDto> DefectReports
);

public sealed record PartnerLabSummaryDto(
    Guid Id,
    string Name,
    string ContactPhone,
    string Address,
    bool IsActive
);


