namespace TheBoys.Application.Payments.DTOs;

public sealed record PaymentAttemptDto(
    Guid Id,
    Guid OrderId,
    string Gateway,
    string MerchantReference,
    string? ProviderTransactionId,
    string Status,
    decimal AmountVnd,
    string Currency,
    DateTime ExpiresAtUtc,
    DateTime CreatedAtUtc,
    DateTime? CapturedAtUtc,
    string? CheckoutUrl,
    string? FailureReason,
    string? QrCode = null
);

