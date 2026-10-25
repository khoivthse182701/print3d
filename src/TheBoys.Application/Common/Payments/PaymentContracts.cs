namespace TheBoys.Application.Common.Payments;

public sealed record CreateCheckoutRequest(
    Guid OrderId,
    string MerchantReference,
    decimal AmountVnd,
    string Currency,
    string Description,
    string ReturnUrl,
    string CancelUrl
);

public sealed record CreateCheckoutResult(
    bool Success,
    string? CheckoutUrl,
    string? ProviderTransactionId,
    string? ErrorCode,
    string? ErrorMessage,
    string? QrCode = null
);

public sealed record QueryPaymentResult(
    bool Success,
    string? ProviderTransactionId,
    string? Status,
    decimal? AmountVnd,
    string? Currency,
    DateTime? TransactionTimeUtc,
    string? RawStatus
);

public sealed record WebhookVerificationRequest(
    string Gateway,
    byte[] RawBody,
    IReadOnlyDictionary<string, string> Headers
);

public sealed record WebhookVerificationResult(
    bool IsValid,
    string? EventId,
    string? MerchantReference,
    string? ProviderTransactionId,
    decimal AmountVnd,
    string Currency,
    string EventStatus,
    DateTime? EventTimeUtc,
    string? FailureReason,
    bool IsTestWebhook = false
);

public sealed record SubmitRefundRequest(
    Guid OrderId,
    Guid PaymentAttemptId,
    Guid OperationKey,
    string MerchantReference,
    string? ProviderTransactionId,
    decimal AmountVnd,
    string Currency,
    string Reason
);

public sealed record SubmitRefundResult(
    bool Success,
    string? ProviderReference,
    string? Status,
    bool IsUncertain,
    string? ErrorCode,
    string? ErrorMessage
);

public sealed record QueryRefundResult(
    bool Success,
    string? ProviderReference,
    string? Status,
    decimal? AmountVnd,
    DateTime? CompletedAtUtc,
    string? RawStatus,
    string? ErrorMessage
);

public interface IPaymentGateway
{
    string GatewayName { get; }
    bool IsConfigured { get; }
    Task<CreateCheckoutResult> CreateCheckoutAsync(CreateCheckoutRequest request, CancellationToken ct);
    Task<QueryPaymentResult> QueryPaymentAsync(string merchantReference, CancellationToken ct);
    Task<WebhookVerificationResult> VerifyWebhookAsync(WebhookVerificationRequest request, CancellationToken ct);
    Task<SubmitRefundResult> SubmitRefundAsync(SubmitRefundRequest request, CancellationToken ct);
    Task<QueryRefundResult> QueryRefundAsync(string merchantReference, Guid operationKey, CancellationToken ct);
}

public interface IPaymentGatewayRegistry
{
    IPaymentGateway ResolveGateway(string gatewayName);
    IPaymentGateway ResolveActiveGateway();
    IReadOnlyList<string> GetSupportedGateways();
}

