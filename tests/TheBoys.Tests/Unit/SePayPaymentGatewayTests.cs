using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Application.Common.Payments;
using TheBoys.Infrastructure.Payments;

namespace TheBoys.Tests.Unit;

public class SePayPaymentGatewayTests
{
    private sealed class FakeClock(DateTime nowUtc) : IClock
    {
        public DateTime UtcNow => nowUtc;
    }

    private static SePayPaymentGateway CreateGateway(
        SePayOptions sePayOptions,
        VietQrOptions? vietQrOptions = null,
        DateTime? nowUtc = null)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new PaymentOptions
        {
            SePay = sePayOptions,
            VietQr = vietQrOptions ?? new VietQrOptions
            {
                BaseUrl = "https://vietqr.app/img",
                BankCode = "MBBank",
                AccountNo = "1234567890",
                AccountName = "THE BOYS 3D PRINTING",
                Template = "compact"
            }
        });

        var clock = new FakeClock(nowUtc ?? new DateTime(2026, 10, 25, 3, 0, 0, DateTimeKind.Utc));
        return new SePayPaymentGateway(options, clock);
    }

    [Fact]
    public void IsConfigured_ReturnsTrue_OnlyWhenApiKeyPresent()
    {
        var unconfigured = CreateGateway(new SePayOptions { ApiKey = "" });
        Assert.False(unconfigured.IsConfigured);

        var configured = CreateGateway(new SePayOptions { ApiKey = "sepay-test-api-key" });
        Assert.True(configured.IsConfigured);
    }

    [Fact]
    public async Task CreateCheckoutAsync_ReturnsVietQrUrl_WhenConfigured()
    {
        var gateway = CreateGateway(new SePayOptions { ApiKey = "sepay-test-api-key" });

        var req = new CreateCheckoutRequest(
            OrderId: Guid.NewGuid(),
            MerchantReference: "TB-REF-12345",
            AmountVnd: 390000m,
            Currency: "VND",
            Description: "Thanh toán đơn hàng",
            ReturnUrl: "http://localhost:5173/success",
            CancelUrl: "http://localhost:5173/cancel"
        );

        var result = await gateway.CreateCheckoutAsync(req, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.CheckoutUrl);
        Assert.Contains("https://vietqr.app/img/MBBank-1234567890-compact.png", result.CheckoutUrl);
        Assert.Contains("amount=390000", result.CheckoutUrl);
        Assert.Contains("addInfo=TB-REF-12345", result.CheckoutUrl);
    }

    [Fact]
    public async Task CreateCheckoutAsync_Fails_WhenNotConfigured()
    {
        var gateway = CreateGateway(new SePayOptions { ApiKey = "" });

        var req = new CreateCheckoutRequest(
            OrderId: Guid.NewGuid(),
            MerchantReference: "TB-REF-12345",
            AmountVnd: 390000m,
            Currency: "VND",
            Description: "Thanh toán",
            ReturnUrl: "http://localhost:5173/success",
            CancelUrl: "http://localhost:5173/cancel"
        );

        var result = await gateway.CreateCheckoutAsync(req, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("GatewayNotConfigured", result.ErrorCode);
    }

    [Fact]
    public async Task VerifyWebhookAsync_ValidApikeyHeader_ExtractsTransactionData()
    {
        var apiKey = "sepay-secret-token-999";
        var gateway = CreateGateway(new SePayOptions
        {
            ApiKey = apiKey,
            InvoicePrefix = "TB"
        });

        var payload = new
        {
            id = 54321,
            gateway = "MBBank",
            transactionDate = "2026-10-25 10:00:00",
            accountNumber = "1234567890",
            code = "FT2610250001",
            content = "Chuyen tien don hang TB-REF-12345",
            transferType = "in",
            transferAmount = 390000m,
            accumulated = 5000000m,
            subAccount = "001",
            referenceCode = "FT2610250001",
            description = "Chuyen tien don hang TB-REF-12345"
        };

        var rawBody = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
        var headers = new Dictionary<string, string>
        {
            ["Authorization"] = $"Apikey {apiKey}"
        };

        var req = new WebhookVerificationRequest("SePay", rawBody, headers);
        var result = await gateway.VerifyWebhookAsync(req, CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Equal("54321", result.EventId);
        Assert.Equal("TB", result.MerchantReference?.Substring(0, 2));
        Assert.Equal(390000m, result.AmountVnd);
        Assert.Equal("VND", result.Currency);
        Assert.Equal("Succeeded", result.EventStatus);
        Assert.Null(result.FailureReason);
    }

    [Fact]
    public async Task VerifyWebhookAsync_BearerHeader_AlsoAccepted()
    {
        var apiKey = "sepay-secret-token-999";
        var gateway = CreateGateway(new SePayOptions { ApiKey = apiKey });

        var payload = new
        {
            id = 998877,
            content = "TB-REF-8888",
            transferType = "in",
            transferAmount = 150000m
        };

        var rawBody = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
        var headers = new Dictionary<string, string>
        {
            ["Authorization"] = $"Bearer {apiKey}"
        };

        var req = new WebhookVerificationRequest("SePay", rawBody, headers);
        var result = await gateway.VerifyWebhookAsync(req, CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Equal("998877", result.EventId);
        Assert.Equal("TB-REF-8888", result.MerchantReference);
        Assert.Equal(150000m, result.AmountVnd);
        Assert.Equal("Succeeded", result.EventStatus);
    }

    [Fact]
    public async Task VerifyWebhookAsync_InvalidApiKey_FailsVerification()
    {
        var gateway = CreateGateway(new SePayOptions { ApiKey = "correct-api-key" });

        var payload = new
        {
            id = 1234,
            content = "TB-REF-123",
            transferType = "in",
            transferAmount = 390000m
        };

        var rawBody = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
        var headers = new Dictionary<string, string>
        {
            ["Authorization"] = "Apikey wrong-api-key"
        };

        var req = new WebhookVerificationRequest("SePay", rawBody, headers);
        var result = await gateway.VerifyWebhookAsync(req, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains("xác thực Authorization thất bại", result.FailureReason);
    }

    [Fact]
    public async Task VerifyWebhookAsync_MissingId_FailsVerification()
    {
        var apiKey = "sepay-key";
        var gateway = CreateGateway(new SePayOptions { ApiKey = apiKey });

        var payload = new
        {
            content = "TB-REF-123",
            transferType = "in",
            transferAmount = 390000m
        };

        var rawBody = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
        var headers = new Dictionary<string, string>
        {
            ["Authorization"] = $"Apikey {apiKey}"
        };

        var req = new WebhookVerificationRequest("SePay", rawBody, headers);
        var result = await gateway.VerifyWebhookAsync(req, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains("thiếu id", result.FailureReason);
    }

    [Fact]
    public async Task SubmitRefundAsync_ReturnsNeedsAttention_AsExpected()
    {
        var gateway = CreateGateway(new SePayOptions { ApiKey = "sepay-key" });

        var req = new SubmitRefundRequest(
            OrderId: Guid.NewGuid(),
            PaymentAttemptId: Guid.NewGuid(),
            OperationKey: Guid.NewGuid(),
            MerchantReference: "TB-REF-1",
            ProviderTransactionId: "TX-1",
            AmountVnd: 390000m,
            Currency: "VND",
            Reason: "Khách hủy đơn"
        );

        var result = await gateway.SubmitRefundAsync(req, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("NeedsAttention", result.Status);
        Assert.True(result.IsUncertain);
        Assert.Contains("ngân hàng", result.ErrorMessage);
    }

    [Fact]
    public void Registry_ResolvesSePay_CaseInsensitively()
    {
        var sepay = CreateGateway(new SePayOptions { ApiKey = "key" });
        var sandbox = new SandboxPaymentGateway(new FakeClock(DateTime.UtcNow));

        var reg = new PaymentGatewayRegistry(
            [sepay, sandbox],
            Microsoft.Extensions.Options.Options.Create(new PaymentOptions { ActiveGateway = "SePay" }));

        Assert.Equal("SePay", reg.ResolveGateway("SePay").GatewayName);
        Assert.Equal("SePay", reg.ResolveGateway("sepay").GatewayName);
        Assert.Equal("SePay", reg.ResolveGateway("SEPAY").GatewayName);
        Assert.Equal("SePay", reg.ResolveActiveGateway().GatewayName);
    }
}

