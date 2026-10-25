using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Application.Common.Payments;

namespace TheBoys.Infrastructure.Payments;

public sealed class PayOSPaymentGateway(
    IOptions<PaymentOptions> options,
    IClock clock,
    IHttpClientFactory httpClientFactory) : IPaymentGateway
{
    private readonly PaymentOptions _paymentOptions = options.Value;
    private readonly PayOsOptions _payOsOptions = options.Value.PayOS;

    public string GatewayName => "PayOS";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_payOsOptions.ClientId) &&
        !string.IsNullOrWhiteSpace(_payOsOptions.ApiKey) &&
        !string.IsNullOrWhiteSpace(_payOsOptions.ChecksumKey);

    public async Task<CreateCheckoutResult> CreateCheckoutAsync(CreateCheckoutRequest request, CancellationToken ct)
    {
        var isTestMode = string.IsNullOrWhiteSpace(_payOsOptions.ClientId) ||
                         _payOsOptions.ClientId.StartsWith("test-", StringComparison.OrdinalIgnoreCase);

        if (!IsConfigured && !isTestMode)
        {
            return new CreateCheckoutResult(
                Success: false,
                CheckoutUrl: null,
                ProviderTransactionId: null,
                ErrorCode: "GatewayNotConfigured",
                ErrorMessage: "Cổng thanh toán PayOS chưa được cấu hình đầy đủ ClientId, ApiKey, và ChecksumKey."
            );
        }

        var orderCode = GenerateOrderCode(request.MerchantReference);

        if (isTestMode)
        {
            // Chế độ mô phỏng / offline kiểm thử tự động: Sinh VietQR URL deterministically không phụ thuộc internet
            var amount = (long)Math.Round(request.AmountVnd);
            var vietQr = _paymentOptions.VietQr;
            var qrUrl = $"{vietQr.BaseUrl}/{vietQr.BankCode}-{vietQr.AccountNo}-{vietQr.Template}.png?amount={amount}&addInfo={Uri.EscapeDataString(request.MerchantReference)}&accountName={Uri.EscapeDataString(vietQr.AccountName)}";

            return new CreateCheckoutResult(
                Success: true,
                CheckoutUrl: qrUrl,
                ProviderTransactionId: $"PAYOS-SIM-{orderCode}",
                ErrorCode: null,
                ErrorMessage: null
            );
        }

        // Chế độ Live: Gọi API tạo link thanh toán PayOS POST /v2/payment-requests
        try
        {
            var amount = (long)Math.Round(request.AmountVnd);
            var returnUrl = !string.IsNullOrWhiteSpace(request.ReturnUrl) ? request.ReturnUrl : _payOsOptions.ReturnUrl;
            var cancelUrl = !string.IsNullOrWhiteSpace(request.CancelUrl) ? request.CancelUrl : _payOsOptions.CancelUrl;

            // PayOS giới hạn description tối đa 25 ký tự
            var description = request.MerchantReference;
            if (description.Length > 25)
            {
                description = description[..25];
            }

            var dataToSign = $"amount={amount}&cancelUrl={cancelUrl}&description={description}&orderCode={orderCode}&returnUrl={returnUrl}";
            var signature = ComputeHmacSha256(dataToSign, _payOsOptions.ChecksumKey);

            var payload = new
            {
                orderCode,
                amount,
                description,
                cancelUrl,
                returnUrl,
                signature
            };

            var client = httpClientFactory.CreateClient("PayOS");
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{_payOsOptions.BaseUrl.TrimEnd('/')}/v2/payment-requests")
            {
                Content = JsonContent.Create(payload)
            };
            requestMessage.Headers.Add("x-client-id", _payOsOptions.ClientId);
            requestMessage.Headers.Add("x-api-key", _payOsOptions.ApiKey);

            var response = await client.SendAsync(requestMessage, ct);
            var responseContent = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                return new CreateCheckoutResult(
                    Success: false,
                    CheckoutUrl: null,
                    ProviderTransactionId: null,
                    ErrorCode: $"PayOsHttpError_{(int)response.StatusCode}",
                    ErrorMessage: $"PayOS API trả mã lỗi HTTP {(int)response.StatusCode}: {responseContent}"
                );
            }

            using var doc = JsonDocument.Parse(responseContent);
            var code = doc.RootElement.TryGetProperty("code", out var codeProp) ? codeProp.GetString() : null;
            if (code != "00")
            {
                var desc = doc.RootElement.TryGetProperty("desc", out var descProp) ? descProp.GetString() : "Không rõ lỗi";
                return new CreateCheckoutResult(
                    Success: false,
                    CheckoutUrl: null,
                    ProviderTransactionId: null,
                    ErrorCode: $"PayOsCode_{code}",
                    ErrorMessage: $"PayOS từ chối tạo link: {desc}"
                );
            }

            var data = doc.RootElement.GetProperty("data");
            var checkoutUrl = data.TryGetProperty("checkoutUrl", out var urlProp) ? urlProp.GetString() : null;
            var paymentLinkId = data.TryGetProperty("paymentLinkId", out var linkProp) ? linkProp.GetString() : orderCode.ToString();
            var qrCode = data.TryGetProperty("qrCode", out var qrProp) ? qrProp.GetString() : null;

            var bin = data.TryGetProperty("bin", out var binProp) ? binProp.GetString() : null;
            var accNo = data.TryGetProperty("accountNumber", out var accProp) ? accProp.GetString() : null;
            var accName = data.TryGetProperty("accountName", out var nameProp) ? nameProp.GetString() : null;

            string? finalQrUrl = null;
            if (!string.IsNullOrWhiteSpace(bin) && !string.IsNullOrWhiteSpace(accNo))
            {
                finalQrUrl = $"https://img.vietqr.io/image/{bin}-{accNo}-compact2.png?amount={amount}&addInfo={Uri.EscapeDataString(description)}&accountName={Uri.EscapeDataString(accName ?? "")}";
            }
            else if (!string.IsNullOrWhiteSpace(qrCode))
            {
                finalQrUrl = $"https://api.qrserver.com/v1/create-qr-code/?size=350x350&data={Uri.EscapeDataString(qrCode)}";
            }

            return new CreateCheckoutResult(
                Success: true,
                CheckoutUrl: checkoutUrl,
                ProviderTransactionId: paymentLinkId,
                ErrorCode: null,
                ErrorMessage: null,
                QrCode: finalQrUrl ?? qrCode
            );
        }
        catch (Exception ex)
        {
            return new CreateCheckoutResult(
                Success: false,
                CheckoutUrl: null,
                ProviderTransactionId: null,
                ErrorCode: "PayOsApiException",
                ErrorMessage: $"Lỗi khi gọi API PayOS: {ex.Message}"
            );
        }
    }

    public Task<WebhookVerificationResult> VerifyWebhookAsync(WebhookVerificationRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_payOsOptions.ChecksumKey))
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
                FailureReason: "Cổng thanh toán PayOS chưa được cấu hình ChecksumKey."
            ));
        }

        try
        {
            using var doc = JsonDocument.Parse(request.RawBody);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
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
                    FailureReason: "Payload webhook không phải JSON object hợp lệ."
                ));
            }

            if (!root.TryGetProperty("signature", out var sigProp) || string.IsNullOrWhiteSpace(sigProp.GetString()))
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
                    FailureReason: "Payload webhook PayOS thiếu trường signature."
                ));
            }

            var incomingSignature = sigProp.GetString()!.Trim();

            if (!root.TryGetProperty("data", out var dataProp) || dataProp.ValueKind != JsonValueKind.Object)
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
                    FailureReason: "Payload webhook PayOS thiếu trường data."
                ));
            }

            // 1. Tính toán chữ ký từ data theo chuẩn PayOS (sort key alphabet, query string format)
            var expectedSignature = ComputeWebhookSignature(dataProp, _payOsOptions.ChecksumKey);

            var incomingBytes = Encoding.UTF8.GetBytes(incomingSignature.ToLowerInvariant());
            var expectedBytes = Encoding.UTF8.GetBytes(expectedSignature.ToLowerInvariant());

            if (incomingBytes.Length != expectedBytes.Length ||
                !CryptographicOperations.FixedTimeEquals(incomingBytes, expectedBytes))
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
                    FailureReason: "Chữ ký webhook PayOS không hợp lệ."
                ));
            }

            // 2. Trích xuất thông tin giao dịch
            long? orderCode = null;
            if (dataProp.TryGetProperty("orderCode", out var ocProp) && ocProp.TryGetInt64(out var ocVal))
            {
                orderCode = ocVal;
            }

            decimal amount = 0m;
            if (dataProp.TryGetProperty("amount", out var amtProp) && amtProp.TryGetDecimal(out var amtVal))
            {
                amount = amtVal;
            }

            var description = dataProp.TryGetProperty("description", out var descProp) ? descProp.GetString() : null;
            var reference = dataProp.TryGetProperty("reference", out var refProp) ? refProp.GetString() : null;
            var paymentLinkId = dataProp.TryGetProperty("paymentLinkId", out var plProp) ? plProp.GetString() : null;
            var currency = dataProp.TryGetProperty("currency", out var currProp) ? currProp.GetString() ?? "VND" : "VND";
            var code = dataProp.TryGetProperty("code", out var codeProp) ? codeProp.GetString() : "00";

            DateTime? eventTime = null;
            if (dataProp.TryGetProperty("transactionDateTime", out var timeProp) && timeProp.ValueKind == JsonValueKind.String)
            {
                if (DateTime.TryParse(timeProp.GetString(), out var parsedTime))
                {
                    eventTime = DateTime.SpecifyKind(parsedTime, DateTimeKind.Utc);
                }
            }
            eventTime ??= clock.UtcNow;

            var isTestPing = orderCode == 123 ||
                             string.Equals(description, "VQRIO123", StringComparison.OrdinalIgnoreCase);

            var isSuccess = string.Equals(code, "00", StringComparison.OrdinalIgnoreCase);
            var eventStatus = isSuccess ? "Succeeded" : "Failed";
            var eventId = orderCode?.ToString() ?? reference ?? Guid.NewGuid().ToString("N");
            var merchantRef = description ?? orderCode?.ToString() ?? "";
            var providerTxId = reference ?? paymentLinkId ?? orderCode?.ToString();

            return Task.FromResult(new WebhookVerificationResult(
                IsValid: true,
                EventId: eventId,
                MerchantReference: merchantRef,
                ProviderTransactionId: providerTxId,
                AmountVnd: amount,
                Currency: currency,
                EventStatus: eventStatus,
                EventTimeUtc: eventTime,
                FailureReason: isSuccess ? null : $"PayOS mã lỗi giao dịch: {code}",
                IsTestWebhook: isTestPing
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
                FailureReason: $"Lỗi giải mã webhook payload PayOS: {ex.Message}"
            ));
        }
    }

    public async Task<QueryPaymentResult> QueryPaymentAsync(string merchantReference, CancellationToken ct)
    {
        if (!IsConfigured)
        {
            return new QueryPaymentResult(
                Success: false,
                ProviderTransactionId: null,
                Status: "Failed",
                AmountVnd: null,
                Currency: "VND",
                TransactionTimeUtc: clock.UtcNow,
                RawStatus: "NOT_CONFIGURED"
            );
        }

        var isTestMode = _payOsOptions.ClientId.StartsWith("test-", StringComparison.OrdinalIgnoreCase);
        if (isTestMode)
        {
            return new QueryPaymentResult(
                Success: false,
                ProviderTransactionId: null,
                Status: "NeedsAttention",
                AmountVnd: null,
                Currency: "VND",
                TransactionTimeUtc: clock.UtcNow,
                RawStatus: "QUERY_NOT_SUPPORTED_OFFLINE"
            );
        }

        try
        {
            var orderCode = GenerateOrderCode(merchantReference);
            var client = httpClientFactory.CreateClient("PayOS");
            var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"{_payOsOptions.BaseUrl.TrimEnd('/')}/v2/payment-requests/{orderCode}");
            requestMessage.Headers.Add("x-client-id", _payOsOptions.ClientId);
            requestMessage.Headers.Add("x-api-key", _payOsOptions.ApiKey);

            var response = await client.SendAsync(requestMessage, ct);
            var responseContent = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                return new QueryPaymentResult(
                    Success: false,
                    ProviderTransactionId: null,
                    Status: "Failed",
                    AmountVnd: null,
                    Currency: "VND",
                    TransactionTimeUtc: clock.UtcNow,
                    RawStatus: $"HTTP_{(int)response.StatusCode}"
                );
            }

            using var doc = JsonDocument.Parse(responseContent);
            var code = doc.RootElement.TryGetProperty("code", out var cProp) ? cProp.GetString() : null;
            if (code != "00")
            {
                return new QueryPaymentResult(
                    Success: false,
                    ProviderTransactionId: null,
                    Status: "Failed",
                    AmountVnd: null,
                    Currency: "VND",
                    TransactionTimeUtc: clock.UtcNow,
                    RawStatus: $"PAYOS_ERROR_{code}"
                );
            }

            var data = doc.RootElement.GetProperty("data");
            var status = data.TryGetProperty("status", out var sProp) ? sProp.GetString() : "PENDING";
            var amount = data.TryGetProperty("amount", out var aProp) ? aProp.GetDecimal() : 0m;
            var paymentLinkId = data.TryGetProperty("paymentLinkId", out var plProp) ? plProp.GetString() : orderCode.ToString();

            var normalizedStatus = status switch
            {
                "PAID" => "Succeeded",
                "PENDING" => "Pending",
                "CANCELLED" => "Failed",
                _ => "Unknown"
            };

            return new QueryPaymentResult(
                Success: normalizedStatus == "Succeeded",
                ProviderTransactionId: paymentLinkId,
                Status: normalizedStatus,
                AmountVnd: amount,
                Currency: "VND",
                TransactionTimeUtc: clock.UtcNow,
                RawStatus: status
            );
        }
        catch (Exception)
        {
            return new QueryPaymentResult(
                Success: false,
                ProviderTransactionId: null,
                Status: "NeedsAttention",
                AmountVnd: null,
                Currency: "VND",
                TransactionTimeUtc: clock.UtcNow,
                RawStatus: "QUERY_EXCEPTION"
            );
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
                ErrorMessage: "Cổng thanh toán PayOS chưa được cấu hình."
            ));
        }

        return Task.FromResult(new SubmitRefundResult(
            Success: false,
            ProviderReference: null,
            Status: "NeedsAttention",
            IsUncertain: true,
            ErrorCode: "PayOsRefundNotSupported",
            ErrorMessage: "PayOS chưa hỗ trợ API hoàn tiền tự động trực tiếp, yêu cầu đối soát và hoàn tiền thủ công qua ngân hàng."
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
                ErrorMessage: "Cổng thanh toán PayOS chưa được cấu hình."
            ));
        }

        return Task.FromResult(new QueryRefundResult(
            Success: false,
            ProviderReference: null,
            Status: "NeedsAttention",
            AmountVnd: null,
            CompletedAtUtc: clock.UtcNow,
            RawStatus: "OFFLINE_QUERY_UNSUPPORTED",
            ErrorMessage: "Chưa hỗ trợ tra cứu đối soát hoàn tiền ngoại tuyến cho PayOS."
        ));
    }

    /// <summary>
    /// Tạo chuỗi canonical từ object `data` theo chuẩn PayOS (sắp xếp key alphabet, format key=value nối bằng &).
    /// </summary>
    public static string CanonicalizeWebhookData(JsonElement dataElement)
    {
        var dataPairs = new List<KeyValuePair<string, string>>();
        foreach (var property in dataElement.EnumerateObject())
        {
            string valStr;
            switch (property.Value.ValueKind)
            {
                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    valStr = "";
                    break;
                case JsonValueKind.String:
                    valStr = property.Value.GetString() ?? "";
                    break;
                case JsonValueKind.Number:
                    valStr = property.Value.GetRawText();
                    break;
                case JsonValueKind.True:
                    valStr = "true";
                    break;
                case JsonValueKind.False:
                    valStr = "false";
                    break;
                default:
                    valStr = property.Value.GetRawText();
                    break;
            }
            dataPairs.Add(new KeyValuePair<string, string>(property.Name, valStr));
        }

        dataPairs.Sort((a, b) => string.Compare(a.Key, b.Key, StringComparison.Ordinal));
        return string.Join("&", dataPairs.Select(p => $"{p.Key}={p.Value}"));
    }

    /// <summary>
    /// Tính chữ ký HMAC-SHA256 chuẩn cho webhook PayOS từ JsonElement của data.
    /// </summary>
    public static string ComputeWebhookSignature(JsonElement dataElement, string checksumKey)
    {
        var canonicalString = CanonicalizeWebhookData(dataElement);
        return ComputeHmacSha256(canonicalString, checksumKey);
    }

    /// <summary>
    /// Tính chữ ký HMAC-SHA256 hex string viết thường.
    /// </summary>
    public static string ComputeHmacSha256(string data, string checksumKey)
    {
        var keyBytes = Encoding.UTF8.GetBytes(checksumKey);
        var dataBytes = Encoding.UTF8.GetBytes(data);
        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(dataBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Sinh orderCode dạng số dương (dưới 2^53 - 1) một cách tất định từ merchantReference.
    /// </summary>
    public static long GenerateOrderCode(string merchantReference)
    {
        if (string.IsNullOrWhiteSpace(merchantReference))
        {
            return 100000L;
        }

        if (long.TryParse(merchantReference, out var direct) && direct > 0)
        {
            return direct;
        }

        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(merchantReference));
        var raw = BitConverter.ToInt64(hash, 0) & 0x7FFFFFFFFFFFFFFF;
        return 1000000L + (raw % 9000000000000000L);
    }
}

