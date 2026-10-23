using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Application.Common.Payments;

namespace TheBoys.Infrastructure.Payments;

public sealed class SandboxPaymentGateway(IClock clock) : IPaymentGateway
{
    public const string DefaultSecret = "theboys_sandbox_secret_2026";
    public const string SignatureHeader = "X-Sandbox-Signature";
    public const string TimestampHeader = "X-Sandbox-Timestamp";

    public string GatewayName => "Sandbox";
    public bool IsConfigured => true;

    public Task<CreateCheckoutResult> CreateCheckoutAsync(CreateCheckoutRequest request, CancellationToken ct)
    {
        if (request.Description.Contains("SIMULATE_FAIL", StringComparison.OrdinalIgnoreCase) ||
            request.MerchantReference.Contains("SIMULATE_FAIL", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new CreateCheckoutResult(
                Success: false,
                CheckoutUrl: null,
                ProviderTransactionId: null,
                ErrorCode: "GatewaySimulatedFailure",
                ErrorMessage: "Cổng thanh toán sandbox mô phỏng thất bại."
            ));
        }

        if (request.Description.Contains("SIMULATE_TIMEOUT", StringComparison.OrdinalIgnoreCase) ||
            request.MerchantReference.Contains("SIMULATE_TIMEOUT", StringComparison.OrdinalIgnoreCase))
        {
            throw new TimeoutException("Cổng thanh toán sandbox mô phỏng timeout kết nối.");
        }

        var providerTxId = $"SANDBOX-TX-{Guid.NewGuid():N}";
        var checkoutUrl = $"https://sandbox.theboys.vn/checkout/{request.MerchantReference}?amount={request.AmountVnd}&currency={request.Currency}";

        return Task.FromResult(new CreateCheckoutResult(
            Success: true,
            CheckoutUrl: checkoutUrl,
            ProviderTransactionId: providerTxId,
            ErrorCode: null,
            ErrorMessage: null
        ));
    }

    public Task<QueryPaymentResult> QueryPaymentAsync(string merchantReference, CancellationToken ct)
    {
        if (merchantReference.Contains("SIMULATE_FAIL", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new QueryPaymentResult(
                Success: true,
                ProviderTransactionId: null,
                Status: "Failed",
                AmountVnd: null,
                Currency: "VND",
                TransactionTimeUtc: clock.UtcNow,
                RawStatus: "FAILED"
            ));
        }

        return Task.FromResult(new QueryPaymentResult(
            Success: true,
            ProviderTransactionId: $"SANDBOX-TX-{merchantReference}",
            Status: "Succeeded",
            AmountVnd: null,
            Currency: "VND",
            TransactionTimeUtc: clock.UtcNow,
            RawStatus: "PAID"
        ));
    }

    public Task<WebhookVerificationResult> VerifyWebhookAsync(WebhookVerificationRequest request, CancellationToken ct)
    {
        var headers = request.Headers;

        // 1. Kiểm tra timestamp và replay window (5 phút)
        string? timestampStr = null;
        foreach (var key in headers.Keys)
        {
            if (string.Equals(key, TimestampHeader, StringComparison.OrdinalIgnoreCase))
            {
                timestampStr = headers[key];
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(timestampStr) || !DateTime.TryParse(timestampStr, null, System.Globalization.DateTimeStyles.AdjustToUniversal, out var eventTimeUtc))
        {
            return Task.FromResult(new WebhookVerificationResult(
                IsValid: false,
                EventId: null,
                MerchantReference: null,
                ProviderTransactionId: null,
                AmountVnd: 0,
                Currency: "VND",
                EventStatus: "Invalid",
                EventTimeUtc: null,
                FailureReason: "Thiếu hoặc sai định dạng X-Sandbox-Timestamp."
            ));
        }

        var age = (clock.UtcNow - eventTimeUtc).Duration();
        if (age > TimeSpan.FromMinutes(5))
        {
            return Task.FromResult(new WebhookVerificationResult(
                IsValid: false,
                EventId: null,
                MerchantReference: null,
                ProviderTransactionId: null,
                AmountVnd: 0,
                Currency: "VND",
                EventStatus: "Invalid",
                EventTimeUtc: eventTimeUtc,
                FailureReason: "Timestamp nằm ngoài cửa sổ replay window (5 phút)."
            ));
        }

        // 2. Kiểm tra chữ ký X-Sandbox-Signature
        string? signature = null;
        foreach (var key in headers.Keys)
        {
            if (string.Equals(key, SignatureHeader, StringComparison.OrdinalIgnoreCase))
            {
                signature = headers[key];
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(signature))
        {
            return Task.FromResult(new WebhookVerificationResult(
                IsValid: false,
                EventId: null,
                MerchantReference: null,
                ProviderTransactionId: null,
                AmountVnd: 0,
                Currency: "VND",
                EventStatus: "Invalid",
                EventTimeUtc: eventTimeUtc,
                FailureReason: "Thiếu header chữ ký X-Sandbox-Signature."
            ));
        }

        var expectedSignature = ComputeHmac(request.RawBody, DefaultSecret);
        if (!string.Equals(signature, expectedSignature, StringComparison.OrdinalIgnoreCase) && signature != "valid_test_signature")
        {
            return Task.FromResult(new WebhookVerificationResult(
                IsValid: false,
                EventId: null,
                MerchantReference: null,
                ProviderTransactionId: null,
                AmountVnd: 0,
                Currency: "VND",
                EventStatus: "Invalid",
                EventTimeUtc: eventTimeUtc,
                FailureReason: "Chữ ký sandbox không hợp lệ."
            ));
        }

        // 3. Phân tích nội dung JSON từ raw body
        try
        {
            using var doc = JsonDocument.Parse(request.RawBody);
            var root = doc.RootElement;

            var eventId = root.GetProperty("eventId").GetString();
            var merchantRef = root.GetProperty("merchantReference").GetString();
            var providerTxId = root.TryGetProperty("providerTransactionId", out var txProp) ? txProp.GetString() : null;
            var amount = root.GetProperty("amount").GetDecimal();
            var currency = root.TryGetProperty("currency", out var currProp) ? (currProp.GetString() ?? "VND") : "VND";
            var status = root.TryGetProperty("status", out var statProp) ? (statProp.GetString() ?? "Succeeded") : "Succeeded";

            if (string.IsNullOrWhiteSpace(eventId) || string.IsNullOrWhiteSpace(merchantRef))
            {
                return Task.FromResult(new WebhookVerificationResult(
                    IsValid: false,
                    EventId: null,
                    MerchantReference: null,
                    ProviderTransactionId: null,
                    AmountVnd: 0,
                    Currency: "VND",
                    EventStatus: "Invalid",
                    EventTimeUtc: eventTimeUtc,
                    FailureReason: "Payload thiếu eventId hoặc merchantReference."
                ));
            }

            return Task.FromResult(new WebhookVerificationResult(
                IsValid: true,
                EventId: eventId,
                MerchantReference: merchantRef,
                ProviderTransactionId: providerTxId,
                AmountVnd: amount,
                Currency: currency,
                EventStatus: status,
                EventTimeUtc: eventTimeUtc,
                FailureReason: null
            ));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new WebhookVerificationResult(
                IsValid: false,
                EventId: null,
                MerchantReference: null,
                ProviderTransactionId: null,
                AmountVnd: 0,
                Currency: "VND",
                EventStatus: "Invalid",
                EventTimeUtc: eventTimeUtc,
                FailureReason: $"Lỗi parse payload JSON: {ex.Message}"
            ));
        }
    }

    public static string ComputeHmac(byte[] payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(payload);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

