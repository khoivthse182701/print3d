namespace TheBoys.WebApi.Contracts.Staff;

public sealed class StaffOrdersQueryParameters
{
    public string? Status { get; set; }
    public string? OrderCode { get; set; }
    public string? CreatedFromUtc { get; set; }
    public string? CreatedToUtc { get; set; }
    public string? MinAgeMinutes { get; set; }
    public string? Page { get; set; }
    public string? PageSize { get; set; }
    public string? Sort { get; set; }
}
