namespace TheBoys.Application.Quotes;

public sealed class QuoteOptions
{
    public int TtlMinutes { get; set; } = 30;
    public decimal DefaultShippingFeeVnd { get; set; } = 0;
    public int MaxQuantity { get; set; } = 10;
    public string PolicyVersion { get; set; } = "v1.0";
}

