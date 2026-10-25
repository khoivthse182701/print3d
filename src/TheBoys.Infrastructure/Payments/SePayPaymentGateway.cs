using System.Text.Json;
using Microsoft.Extensions.Options;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Application.Common.Payments;

namespace TheBoys.Infrastructure.Payments;

public sealed class SePayPaymentGateway(
    IOptions<PaymentOptions> options,
    IClock clock) : IPaymentGateway
{
    private readonly SePayOptions _sePayOptions = options.Value.SePay;

    public string GatewayName => "SePay";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_sePayOptions.ApiKey);

    public Task<CreateCheckoutResult> CreateCheckoutAsync(CreateCheckoutRequest request, CancellationToken ct)
    {
        if (!IsConfigured)
        {
            return Task.FromResult(new CreateCheckoutResult(
                Success: false,
                CheckoutUrl: null,
                ProviderTransactionId: null,
                ErrorCode: "GatewayNotConfigured",
                ErrorMessage: "Cổng thanh toán SePay chưa được cấu hình API key."
            ));
        }

        // SePay VietQR URL format
        var amount = (long)Math.Round(request.AmountVnd);
        var vietQr = options.Value.VietQr;
        var qrUrl = $"{vietQr.BaseUrl}/{vietQr.BankCode}-{vietQr.AccountNo}-{vietQr.Template}.png?amount={amount}&addInfo={Uri.EscapeDataString(request.MerchantReference)}&accountName={Uri.EscapeDataString(vietQr.AccountName)}";

        return Task.FromResult(new CreateCheckoutResult(
            Success: true,
            CheckoutUrl: qrUrl,
            ProviderTransactionId: null,
            ErrorCode: null,
            ErrorMessage: null
        ));
    }

    public Task<QueryPaymentResult> QueryPaymentAsync(string merchantReference, CancellationToken ct)
    {
        if (!IsConfigured)
        {
            return Task.FromResult(new QueryPaymentResult(
                Success: false,
                ProviderTransactionId: null,
                Status: "Failed",
                AmountVnd: null,
                Currency: "VND",
                TransactionTimeUtc: clock.UtcNow,
                RawStatus: "NOT_CONFIGURED"
            ));
        }

        // Placeholder for live API query; when offline or without live credentials, fails closed
        return Task.FromResult(new QueryPaymentResult(
            Success: false,
            ProviderTransactionId: null,
            Status: "NeedsAttention",
            AmountVnd: null,
            Currency: "VND",
            TransactionTimeUtc: clock.UtcNow,
            RawStatus: "QUERY_NOT_SUPPORTED_OFFLINE"
        ));
    }

    public Task<WebhookVerificationResult> VerifyWebhookAsync(WebhookVerificationRequest request, CancellationToken ct)
    {
        if (!IsConfigured)
        {
            return Task.FromResult(new WebhookVerificationResult(
                IsValid: false,
                EventId: null,
                MerchantReference: null,
                ProviderTransactionId: null,
                AmountVnd: 0,
                Currency: "VND",
                EventStatus: "Invalid",
                EventTimeUtc: clock.UtcNow,
                FailureReason: "Cổng thanh toán SePay chưa được cấu hình API key."
            ));
        }

        // 1. Kiểm tra Authorization header: "Apikey <Key>" hoặc "Bearer <Key>"
        string? authHeader = null;
        foreach (var key in request.Headers.Keys)
        {
            if (string.Equals(key, "Authorization", StringComparison.OrdinalIgnoreCase))
            {
                authHeader = request.Headers[key];
                break;
            }
        }

        var expectedKey = _sePayOptions.ApiKey.Trim();
        var isAuthorized = false;
        if (!string.IsNullOrWhiteSpace(authHeader))
        {
            var parts = authHeader.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && (string.Equals(parts[0], "Apikey", StringComparison.OrdinalIgnoreCase) ||
                                      string.Equals(parts[0], "Bearer", StringComparison.OrdinalIgnoreCase)))
            {
                isAuthorized = string.Equals(parts[1], expectedKey, StringComparison.Ordinal);
            }
        }

        if (!isAuthorized)
        {
            return Task.FromResult(new WebhookVerificationResult(
                IsValid: false,
                EventId: null,
                MerchantReference: null,
                ProviderTransactionId: null,
                AmountVnd: 0,
                Currency: "VND",
                EventStatus: "Invalid",
                EventTimeUtc: clock.UtcNow,
                FailureReason: "SePay webhook xác thực Authorization thất bại."
            ));
        }

        // 2. Parse SePay webhook JSON payload
        try
        {
            using var doc = JsonDocument.Parse(request.RawBody);
            var root = doc.RootElement;

            // SePay id
            var id = root.TryGetProperty("id", out var idProp) ? idProp.ToString() : null;
            if (string.IsNullOrWhiteSpace(id))
            {
                return Task.FromResult(new WebhookVerificationResult(
                    IsValid: false,
                    EventId: null,
                    MerchantReference: null,
                    ProviderTransactionId: null,
                    AmountVnd: 0,
                    Currency: "VND",
                    EventStatus: "Invalid",
                    EventTimeUtc: clock.UtcNow,
                    FailureReason: "Payload SePay thiếu id giao dịch."
                ));
            }

            var transferAmount = root.TryGetProperty("transferAmount", out var amtProp) ? amtProp.GetDecimal() : 0m;
            var transferType = root.TryGetProperty("transferType", out var typeProp) ? typeProp.GetString() : "in";
            var content = root.TryGetProperty("content", out var contentProp) ? contentProp.GetString() : "";

            // Trích xuất merchant reference từ content
            string? merchantReference = null;
            if (!string.IsNullOrWhiteSpace(content))
            {
                var prefix = _sePayOptions.InvoicePrefix;
                var words = content.Split([' ', '\t', '\r', '\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries);
                foreach (var w in words)
                {
                    var cleaned = w.Trim().TrimEnd('.', ',');
                    if (cleaned.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || cleaned.Length >= 8)
                    {
                        merchantReference = cleaned;
                        break;
                    }
                }

                merchantReference ??= content.Trim();
            }

            var isSuccess = string.Equals(transferType, "in", StringComparison.OrdinalIgnoreCase);

            return Task.FromResult(new WebhookVerificationResult(
                IsValid: true,
                EventId: id,
                MerchantReference: merchantReference,
                ProviderTransactionId: id,
                AmountVnd: transferAmount,
                Currency: "VND",
                EventStatus: isSuccess ? "Succeeded" : "Failed",
                EventTimeUtc: clock.UtcNow,
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
                EventTimeUtc: clock.UtcNow,
                FailureReason: $"Lỗi parse payload SePay: {ex.Message}"
            ));
        }
    }

    public Task<SubmitRefundResult> SubmitRefundAsync(SubmitRefundRequest request, CancellationToken ct)
    {
        if (!IsConfigured)
        {
            return Task.FromResult(new SubmitRefundResult(
                Success: false,
                ProviderReference: null,
                Status: "Failed",
                IsUncertain: false,
                ErrorCode: "GatewayNotConfigured",
                ErrorMessage: "Cổng thanh toán SePay chưa được cấu hình API key."
            ));
        }

        // SePay currently requires banking integration or manual bank disbursement for refund API;
        // In offline/staging without live credentials, fails closed or returns NeedsAttention.
        return Task.FromResult(new SubmitRefundResult(
            Success: false,
            ProviderReference: null,
            Status: "NeedsAttention",
            IsUncertain: true,
            ErrorCode: "OfflineRefundUnsupported",
            ErrorMessage: "SePay yêu cầu liên kết tài khoản ngân hàng để thực hiện hoàn tiền tự động."
        ));
    }

    public Task<QueryRefundResult> QueryRefundAsync(string merchantReference, Guid operationKey, CancellationToken ct)
    {
        if (!IsConfigured)
        {
            return Task.FromResult(new QueryRefundResult(
                Success: false,
                ProviderReference: null,
                Status: "Failed",
                AmountVnd: null,
                CompletedAtUtc: clock.UtcNow,
                RawStatus: "NOT_CONFIGURED",
                ErrorMessage: "Cổng thanh toán SePay chưa được cấu hình API key."
            ));
        }

        return Task.FromResult(new QueryRefundResult(
            Success: false,
            ProviderReference: null,
            Status: "NeedsAttention",
            AmountVnd: null,
            CompletedAtUtc: clock.UtcNow,
            RawStatus: "OFFLINE_QUERY_UNSUPPORTED",
            ErrorMessage: "Chưa hỗ trợ tra cứu đối soát hoàn tiền ngoại tuyến cho SePay."
        ));
    }
}

