using MediatR;
using TheBoys.Application.Staff.Models;
using TheBoys.Domain.Enums;

namespace TheBoys.Application.Staff.Queries.GetStaffOrders;

public sealed record GetStaffOrdersQuery(
    OrderStatus? Status,
    string? OrderCode,
    DateTime? CreatedFromUtc,
    DateTime? CreatedToUtc,
    int? MinAgeMinutes,
    int Page,
    int PageSize,
    string Sort,
    DateTime CapturedUtcNow
) : IRequest<StaffPagedResult<StaffOrderItem>>;
