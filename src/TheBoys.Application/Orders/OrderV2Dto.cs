using TheBoys.Application.Quotes;

namespace TheBoys.Application.Orders;

public sealed record OrderItemDto(
    Guid Id,
    int LineNumber,
    Guid VersionId,
    string ConfigurationHash,
    int Quantity,
    decimal UnitPriceVnd,
    string MaterialSnapshot,
    decimal HeightMm,
    string? EngravingText,
    decimal PartnerUnitBudgetVnd
);

public sealed record OrderStatusHistoryDto(
    Guid Id,
    string Axis,
    string? FromStatus,
    string ToStatus,
    Guid? ActorUserId,
    string Source,
    string Reason,
    string OperationKey,
    DateTime CreatedAtUtc
);

public sealed record OrderV2Dto(
    Guid Id,
    string OrderCode,
    Guid UserId,
    Guid QuoteId,
    string FulfillmentStatus,
    string PaymentStatus,
    int Quantity,
    decimal UnitPriceVnd,
    decimal SubtotalVnd,
    decimal ShippingFeeVnd,
    decimal TotalVnd,
    string Currency,
    ShippingAddressDto Address,
    string Material,
    decimal HeightMm,
    DateTime CreatedAtUtc,
    IReadOnlyList<OrderItemDto> Items,
    IReadOnlyList<OrderStatusHistoryDto> StatusHistories
);

