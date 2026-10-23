namespace TheBoys.Application.Quotes;

public sealed class ShippingAddressDto
{
    public string ReceiverName { get; set; } = default!;
    public string ReceiverPhone { get; set; } = default!;
    public string StreetAddress { get; set; } = default!;
    public string District { get; set; } = default!;
    public string City { get; set; } = "Thành phố Hồ Chí Minh";
    public string? CustomerNote { get; set; }
}

