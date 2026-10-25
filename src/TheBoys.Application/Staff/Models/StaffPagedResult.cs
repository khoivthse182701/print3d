namespace TheBoys.Application.Staff.Models;

public sealed record StaffPagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount
);
