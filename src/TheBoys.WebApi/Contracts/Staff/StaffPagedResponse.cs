namespace TheBoys.WebApi.Contracts.Staff;

public sealed record StaffPagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount
);
