using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Application.Common.Payments;
using TheBoys.Infrastructure.Payments;

namespace TheBoys.Tests.Unit;

public class PayOSPaymentGatewayTests
{
    private sealed class FakeClock(DateTime nowUtc) : IClock
    {
        public DateTime UtcNow => nowUtc;
    }

    private sealed class FakeHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private static PayOSPaymentGateway CreateGateway(
        PayOsOptions payOsOptions,
        VietQrOptions? vietQrOptions = null,
        DateTime? nowUtc = null,
        HttpClient? httpClient = null)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new PaymentOptions
        {
            PayOS = payOsOptions,
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
        var factory = new FakeHttpClientFactory(httpClient ?? new HttpClient());

        return new PayOSPaymentGateway(options, clock, factory);
    }

    [Fact]
    public void IsConfigured_ReturnsTrue_OnlyWhenAllCredentialsPresent()
    {
        var configured = CreateGateway(new PayOsOptions
        {
            ClientId = "test-client-id",
            ApiKey = "test-api-key",
            ChecksumKey = "test-checksum-key"
        });
        Assert.True(configured.IsConfigured);

        var missingClient = CreateGateway(new PayOsOptions
        {
            ClientId = "",
            ApiKey = "test-api-key",
            ChecksumKey = "test-checksum-key"
        });
        Assert.False(missingClient.IsConfigured);

        var missingApiKey = CreateGateway(new PayOsOptions
        {
            ClientId = "test-client-id",
            ApiKey = "  ",
            ChecksumKey = "test-checksum-key"
        });
        Assert.False(missingApiKey.IsConfigured);

        var missingChecksum = CreateGateway(new PayOsOptions
        {
            ClientId = "test-client-id",
            ApiKey = "test-api-key",
            ChecksumKey = ""
        });
        Assert.False(missingChecksum.IsConfigured);
    }

    [Fact]
    public async Task CreateCheckoutAsync_InTestMode_GeneratesOfflineVietQrUrlDeterministically()
    {
        var gateway = CreateGateway(new PayOsOptions
        {
            ClientId = "test-client-id",
            ApiKey = "test-api-key",
            ChecksumKey = "test-checksum-key"
        });

        var request = new CreateCheckoutRequest(
            OrderId: Guid.NewGuid(),
            MerchantReference: "TB-20261025-ORDER01",
            AmountVnd: 390000m,
            Currency: "VND",
            Description: "Thanh toán đơn hàng",
            ReturnUrl: "https://example.com/return",
            CancelUrl: "https://example.com/cancel"
        );

        var result = await gateway.CreateCheckoutAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.CheckoutUrl);
        Assert.Contains("vietqr.app/img", result.CheckoutUrl);
        Assert.Contains("amount=390000", result.CheckoutUrl);
        Assert.Contains("addInfo=TB-20261025-ORDER01", result.CheckoutUrl);
        Assert.StartsWith("PAYOS-SIM-", result.ProviderTransactionId);
    }

    [Fact]
    public void GenerateOrderCode_ProducesDeterministicPositiveLong()
    {
        var ref1 = "TB-20261025-ORDER01";
        var code1 = PayOSPaymentGateway.GenerateOrderCode(ref1);
        var code2 = PayOSPaymentGateway.GenerateOrderCode(ref1);

        Assert.Equal(code1, code2);
        Assert.True(code1 > 0);
        Assert.True(code1 < 9007199254740991L);

        // Numeric string maps directly
        var direct = PayOSPaymentGateway.GenerateOrderCode("123456");
        Assert.Equal(123456L, direct);
    }

    [Fact]
    public async Task VerifyWebhookAsync_ValidSignature_ExtractsTransactionDataCorrectly()
    {
        var checksumKey = "secret-checksum-key-123456";
        var gateway = CreateGateway(new PayOsOptions
        {
            ClientId = "test-client",
            ApiKey = "test-api",
            ChecksumKey = checksumKey
        });

        var dataJson = new
        {
            orderCode = 987654321L,
            amount = 390000,
            description = "TB-20261025-XYZ",
            accountNumber = "1234567890",
            reference = "FT2610250001",
            transactionDateTime = "2026-10-25 03:15:00",
            currency = "VND",
            paymentLinkId = "link-id-123",
            code = "00",
            desc = "success"
        };

        var dataElement = JsonDocument.Parse(JsonSerializer.Serialize(dataJson)).RootElement;
        var signature = PayOSPaymentGateway.ComputeWebhookSignature(dataElement, checksumKey);

        var payload = new
        {
            code = "00",
            desc = "success",
            data = dataJson,
            signature
        };

        var rawBody = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
        var req = new WebhookVerificationRequest("PayOS", rawBody, new Dictionary<string, string>());

        var result = await gateway.VerifyWebhookAsync(req, CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Equal("987654321", result.EventId);
        Assert.Equal("TB-20261025-XYZ", result.MerchantReference);
        Assert.Equal("FT2610250001", result.ProviderTransactionId);
        Assert.Equal(390000m, result.AmountVnd);
        Assert.Equal("VND", result.Currency);
        Assert.Equal("Succeeded", result.EventStatus);
        Assert.False(result.IsTestWebhook);
        Assert.Null(result.FailureReason);
    }

    [Fact]
    public async Task VerifyWebhookAsync_TamperedSignature_ReturnsInvalid()
    {
        var checksumKey = "secret-checksum-key-123456";
        var gateway = CreateGateway(new PayOsOptions
        {
            ClientId = "test-client",
            ApiKey = "test-api",
            ChecksumKey = checksumKey
        });

        var payload = new
        {
            code = "00",
            desc = "success",
            data = new
            {
                orderCode = 987654321L,
                amount = 390000,
                description = "TB-20261025-XYZ"
            },
            signature = "tampered_fake_signature_hex"
        };

        var rawBody = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
        var req = new WebhookVerificationRequest("PayOS", rawBody, new Dictionary<string, string>());

        var result = await gateway.VerifyWebhookAsync(req, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal("Chữ ký webhook PayOS không hợp lệ.", result.FailureReason);
    }

    [Fact]
    public async Task VerifyWebhookAsync_TestPingFromPayOSDashboard_IdentifiedAsTestWebhook()
    {
        var checksumKey = "secret-checksum-key-123456";
        var gateway = CreateGateway(new PayOsOptions
        {
            ClientId = "test-client",
            ApiKey = "test-api",
            ChecksumKey = checksumKey
        });

        // PayOS Dashboard standard test ping payload
        var dataJson = new
        {
            orderCode = 123L,
            amount = 1000,
            description = "VQRIO123",
            accountNumber = "123456789",
            reference = "123",
            transactionDateTime = "2026-01-01 00:00:00",
            currency = "VND",
            paymentLinkId = "test-link-id",
            code = "00",
            desc = "success"
        };

        var dataElement = JsonDocument.Parse(JsonSerializer.Serialize(dataJson)).RootElement;
        var signature = PayOSPaymentGateway.ComputeWebhookSignature(dataElement, checksumKey);

        var payload = new
        {
            code = "00",
            desc = "success",
            data = dataJson,
            signature
        };

        var rawBody = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
        var req = new WebhookVerificationRequest("PayOS", rawBody, new Dictionary<string, string>());

        var result = await gateway.VerifyWebhookAsync(req, CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.True(result.IsTestWebhook);
        Assert.Equal("123", result.EventId);
        Assert.Equal("VQRIO123", result.MerchantReference);
    }

    [Fact]
    public async Task VerifyWebhookAsync_PayOsErrorCode_MarksStatusFailed()
    {
        var checksumKey = "secret-checksum-key-123456";
        var gateway = CreateGateway(new PayOsOptions
        {
            ClientId = "test-client",
            ApiKey = "test-api",
            ChecksumKey = checksumKey
        });

        var dataJson = new
        {
            orderCode = 555666L,
            amount = 100000,
            description = "TB-FAILED-TEST",
            code = "09",
            desc = "Customer cancelled transaction"
        };

        var dataElement = JsonDocument.Parse(JsonSerializer.Serialize(dataJson)).RootElement;
        var signature = PayOSPaymentGateway.ComputeWebhookSignature(dataElement, checksumKey);

        var payload = new
        {
            code = "09",
            desc = "Customer cancelled transaction",
            data = dataJson,
            signature
        };

        var rawBody = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
        var req = new WebhookVerificationRequest("PayOS", rawBody, new Dictionary<string, string>());

        var result = await gateway.VerifyWebhookAsync(req, CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Equal("Failed", result.EventStatus);
        Assert.Contains("PayOS mã lỗi giao dịch: 09", result.FailureReason);
    }

    [Fact]
    public async Task SubmitRefundAsync_ReturnsNeedsAttention_ExplainingOfflineManualDisbursement()
    {
        var gateway = CreateGateway(new PayOsOptions
        {
            ClientId = "test-client",
            ApiKey = "test-api",
            ChecksumKey = "test-checksum"
        });

        var refundReq = new SubmitRefundRequest(
            OrderId: Guid.NewGuid(),
            PaymentAttemptId: Guid.NewGuid(),
            OperationKey: Guid.NewGuid(),
            MerchantReference: "TB-ORD-01",
            ProviderTransactionId: "TX-01",
            AmountVnd: 50000m,
            Currency: "VND",
            Reason: "Khách hàng đổi ý"
        );

        var result = await gateway.SubmitRefundAsync(refundReq, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("NeedsAttention", result.Status);
        Assert.True(result.IsUncertain);
        Assert.Equal("PayOsRefundNotSupported", result.ErrorCode);
    }

    [Fact]
    public async Task QueryPaymentAsync_InTestMode_ReturnsNeedsAttention()
    {
        var gateway = CreateGateway(new PayOsOptions
        {
            ClientId = "test-client",
            ApiKey = "test-api",
            ChecksumKey = "test-checksum"
        });

        var result = await gateway.QueryPaymentAsync("TB-REF-01", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("NeedsAttention", result.Status);
        Assert.Equal("QUERY_NOT_SUPPORTED_OFFLINE", result.RawStatus);
    }
}

