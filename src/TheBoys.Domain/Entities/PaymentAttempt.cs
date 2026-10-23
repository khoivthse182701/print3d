using TheBoys.Domain.Common;
using TheBoys.Domain.Enums;

namespace TheBoys.Domain.Entities;

public sealed class PaymentAttempt : BaseEntity
{
    public Guid OrderId { get; private set; }
    public string Gateway { get; private set; } = default!;
    public string MerchantReference { get; private set; } = default!;
    public string? ProviderTransactionId { get; private set; }
    public PaymentAttemptStatus Status { get; private set; } = PaymentAttemptStatus.Pending;
    public decimal AmountVnd { get; private set; }
    public string Currency { get; private set; } = "VND";
    public decimal? GatewayFeeActualVnd { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? CapturedAtUtc { get; private set; }
    public string? CheckoutUrl { get; private set; }
    public string? FailureReason { get; private set; }
    public string? ClientRequestId { get; private set; }
    public string? RequestFingerprint { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    // Navigation
    public Order Order { get; private set; } = default!;
    public ICollection<PaymentEvent> Events { get; private set; } = new List<PaymentEvent>();

    private PaymentAttempt() { }

    public static PaymentAttempt Create(
        Guid orderId,
        string gateway,
        string merchantReference,
        decimal amountVnd,
        string currency,
        DateTime expiresAtUtc,
        DateTime createdAtUtc,
        string? clientRequestId = null,
        string? requestFingerprint = null)
    {
        if (orderId == Guid.Empty) throw new ArgumentException("OrderId không được rỗng.", nameof(orderId));
        if (string.IsNullOrWhiteSpace(gateway)) throw new ArgumentException("Gateway không được rỗng.", nameof(gateway));
        if (string.IsNullOrWhiteSpace(merchantReference)) throw new ArgumentException("MerchantReference không được rỗng.", nameof(merchantReference));
        if (amountVnd <= 0) throw new ArgumentException("AmountVnd phải lớn hơn 0.", nameof(amountVnd));
        if (string.IsNullOrWhiteSpace(currency) || currency.Trim().ToUpperInvariant() != "VND")
            throw new ArgumentException("Currency phải là 'VND'.", nameof(currency));
        if (expiresAtUtc <= createdAtUtc) throw new ArgumentException("ExpiresAtUtc phải sau CreatedAtUtc.", nameof(expiresAtUtc));

        return new PaymentAttempt
        {
            OrderId = orderId,
            Gateway = gateway.Trim(),
            MerchantReference = merchantReference.Trim(),
            Status = PaymentAttemptStatus.Pending,
            AmountVnd = amountVnd,
            Currency = "VND",
            ExpiresAtUtc = expiresAtUtc,
            CreatedAtUtc = createdAtUtc,
            ClientRequestId = clientRequestId?.Trim(),
            RequestFingerprint = requestFingerprint?.Trim()
        };
    }

    public void SetCheckoutAction(string? checkoutUrl, string? providerTransactionId)
    {
        CheckoutUrl = checkoutUrl;
        if (!string.IsNullOrWhiteSpace(providerTransactionId))
        {
            ProviderTransactionId = providerTransactionId.Trim();
        }
    }

    public void MarkSucceeded(string? providerTransactionId, DateTime capturedAtUtc, decimal? gatewayFeeActualVnd = null)
    {
        Status = PaymentAttemptStatus.Succeeded;
        if (!string.IsNullOrWhiteSpace(providerTransactionId))
        {
            ProviderTransactionId = providerTransactionId.Trim();
        }
        CapturedAtUtc = capturedAtUtc;
        if (gatewayFeeActualVnd.HasValue && gatewayFeeActualVnd.Value >= 0)
        {
            GatewayFeeActualVnd = gatewayFeeActualVnd.Value;
        }
    }

    public void MarkFailed(string? failureReason)
    {
        Status = PaymentAttemptStatus.Failed;
        FailureReason = failureReason;
    }

    public void MarkExpired()
    {
        Status = PaymentAttemptStatus.Expired;
    }

    public void MarkNeedsAttention(string? reason)
    {
        Status = PaymentAttemptStatus.NeedsAttention;
        FailureReason = reason;
    }
}

