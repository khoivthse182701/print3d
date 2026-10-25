using Microsoft.Extensions.Options;
using TheBoys.Application.Common.Payments;

namespace TheBoys.Infrastructure.Payments;

public sealed class PaymentGatewayRegistry : IPaymentGatewayRegistry
{
    private readonly Dictionary<string, IPaymentGateway> _gateways;
    private readonly string _activeGatewayName;

    public PaymentGatewayRegistry(
        IEnumerable<IPaymentGateway> gateways,
        IOptions<PaymentOptions> options)
    {
        _gateways = gateways.ToDictionary(g => g.GatewayName, StringComparer.OrdinalIgnoreCase);
        _activeGatewayName = options.Value.ActiveGateway ?? "Sandbox";
    }

    public IPaymentGateway ResolveGateway(string gatewayName)
    {
        if (string.IsNullOrWhiteSpace(gatewayName))
            throw new ArgumentException("Tên gateway không được rỗng.", nameof(gatewayName));

        if (_gateways.TryGetValue(gatewayName.Trim(), out var gateway))
        {
            return gateway;
        }

        throw new KeyNotFoundException($"Không tìm thấy payment gateway '{gatewayName}'. Các gateway hỗ trợ: {string.Join(", ", _gateways.Keys)}");
    }

    public IPaymentGateway ResolveActiveGateway()
    {
        return ResolveGateway(_activeGatewayName);
    }

    public IReadOnlyList<string> GetSupportedGateways()
    {
        return _gateways.Keys.ToList();
    }
}

