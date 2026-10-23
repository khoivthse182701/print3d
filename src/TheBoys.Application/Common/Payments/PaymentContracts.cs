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
    string? ErrorMessage
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
    string? FailureReason
);

public interface IPaymentGateway
{
    string GatewayName { get; }
    bool IsConfigured { get; }
    Task<CreateCheckoutResult> CreateCheckoutAsync(CreateCheckoutRequest request, CancellationToken ct);
    Task<QueryPaymentResult> QueryPaymentAsync(string merchantReference, CancellationToken ct);
    Task<WebhookVerificationResult> VerifyWebhookAsync(WebhookVerificationRequest request, CancellationToken ct);
}

public interface IPaymentGatewayRegistry
{
    IPaymentGateway ResolveGateway(string gatewayName);
    IPaymentGateway ResolveActiveGateway();
    IReadOnlyList<string> GetSupportedGateways();
}

