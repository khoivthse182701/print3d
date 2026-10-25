namespace TheBoys.Infrastructure.Payments;

public sealed class PaymentOptions
{
    public const string SectionName = "PaymentSettings";

    public string ActiveGateway { get; set; } = "Sandbox";
    public VietQrOptions VietQr { get; set; } = new();
    public SePayOptions SePay { get; set; } = new();
    public PayOsOptions PayOS { get; set; } = new();
}

public sealed class VietQrOptions
{
    public string BaseUrl { get; set; } = "https://vietqr.app/img";
    public string BankCode { get; set; } = "MBBank";
    public string AccountNo { get; set; } = "";
    public string AccountName { get; set; } = "THE BOYS 3D PRINTING";
    public string Template { get; set; } = "compact";
    public bool ShowInfo { get; set; } = true;
}

public sealed class SePayOptions
{
    public string ApiKey { get; set; } = "";
    public string InvoicePrefix { get; set; } = "TB";
    public string BaseUrl { get; set; } = "https://my.sepay.vn";
}

public sealed class PayOsOptions
{
    public string ClientId { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string ChecksumKey { get; set; } = "";
    public string BaseUrl { get; set; } = "https://api-merchant.payos.vn";
    public string ReturnUrl { get; set; } = "";
    public string CancelUrl { get; set; } = "";
    public int WebhookReplayWindowSeconds { get; set; } = 300;
}

