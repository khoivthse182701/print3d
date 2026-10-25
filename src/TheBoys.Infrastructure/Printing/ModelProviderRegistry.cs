using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using TheBoys.Application.Printing;

namespace TheBoys.Infrastructure.Printing;

public sealed class ModelProviderRegistry : IModelProviderRegistry
{
    private readonly Dictionary<string, IModelGenerationProvider> _providers;
    private readonly IConfiguration _configuration;
    private readonly IOptions<MeshyOptions> _meshyOptions;
    private readonly IOptions<TripoOptions> _tripoOptions;
    private readonly IOptions<Hunyuan3DOptions> _hunyuanOptions;

    public ModelProviderRegistry(
        MeshyProvider meshy,
        TripoProvider tripo,
        Hunyuan3DProvider hunyuan,
        IConfiguration configuration,
        IOptions<MeshyOptions> meshyOptions,
        IOptions<TripoOptions> tripoOptions,
        IOptions<Hunyuan3DOptions> hunyuanOptions)
    {
        _providers = new Dictionary<string, IModelGenerationProvider>(StringComparer.OrdinalIgnoreCase)
        {
            [meshy.Name] = meshy,
            [tripo.Name] = tripo,
            [hunyuan.Name] = hunyuan,
            ["Hunyuan"] = hunyuan,
            ["Tencent"] = hunyuan,
            ["hy3d"] = hunyuan
        };
        if (!string.IsNullOrWhiteSpace(hunyuan.Model) && !_providers.ContainsKey(hunyuan.Model))
        {
            _providers[hunyuan.Model] = hunyuan;
        }
        _configuration = configuration;
        _meshyOptions = meshyOptions;
        _tripoOptions = tripoOptions;
        _hunyuanOptions = hunyuanOptions;
    }

    public IModelGenerationProvider Resolve(string providerKey)
    {
        if (string.IsNullOrWhiteSpace(providerKey))
            throw new ProviderException("Provider key không được để trống.", uncertain: false, transient: false);

        if (!_providers.TryGetValue(providerKey, out var provider))
            throw new ProviderException($"Provider '{providerKey}' không được hỗ trợ hoặc adapter chưa được đăng ký trong hệ thống.", uncertain: false, transient: false);

        if (!provider.IsConfigured)
            throw new ProviderException($"Provider '{providerKey}' chưa được cấu hình API key trên máy chủ.", uncertain: false, transient: false);

        return provider;
    }

    public IModelGenerationProvider ResolveForNewJob(string? requestedProviderKey = null, string? requestedCapability = null)
    {
        var targetKey = string.IsNullOrWhiteSpace(requestedProviderKey)
            ? (_configuration["AiProviders:Active"] ?? "Meshy")
            : requestedProviderKey.Trim();

        var caps = GetCapabilities(targetKey);
        if (!caps.IsEnabledForNewJobs)
            throw new ProviderException($"Provider '{targetKey}' hiện đang bị vô hiệu hóa hoặc chưa được cấu hình cho các tác vụ mới.", uncertain: false, transient: false);

        if (!string.IsNullOrWhiteSpace(requestedCapability))
        {
            var supported = requestedCapability.ToLowerInvariant() switch
            {
                "imageto3d" => caps.SupportsImageTo3D,
                "textto3d" => caps.SupportsTextTo3D,
                "resize" => caps.SupportsResize,
                "convert" => caps.SupportsConvert,
                "analyze" => caps.SupportsAnalyze,
                _ => false
            };

            if (!supported)
                throw new ProviderException($"Provider '{targetKey}' không hỗ trợ khả năng '{requestedCapability}'.", uncertain: false, transient: false);
        }

        return Resolve(targetKey);
    }

    public ProviderCapabilities GetCapabilities(string providerKey, string? modelKey = null)
    {
        if (string.IsNullOrWhiteSpace(providerKey))
            throw new ProviderException("Provider key không được để trống.");

        if (string.Equals(providerKey, "Meshy", StringComparison.OrdinalIgnoreCase))
        {
            var isExplicitlyDisabled = string.Equals(_configuration["AiProviders:Meshy:EnabledForNewJobs"], "false", StringComparison.OrdinalIgnoreCase);
            var isConfigured = !string.IsNullOrWhiteSpace(_meshyOptions.Value.ApiKey);
            return new ProviderCapabilities(
                ProviderKey: "Meshy",
                ModelKey: modelKey ?? _meshyOptions.Value.DefaultEngine ?? "meshy-6-lite",
                ApiVersion: "v1",
                PricingVersion: "2026.01",
                AccountScope: "default",
                SupportsImageTo3D: true,
                SupportsTextTo3D: false,
                SupportsTexture: true,
                SupportsResize: true,
                SupportsConvert: true,
                SupportsAnalyze: true,
                MinHeightMm: 100,
                MaxHeightMm: 120,
                AllowedFormats: ["glb", "stl"],
                IsEnabledForNewJobs: !isExplicitlyDisabled && isConfigured
            );
        }

        if (string.Equals(providerKey, "Tripo", StringComparison.OrdinalIgnoreCase))
        {
            var isExplicitlyDisabled = string.Equals(_configuration["AiProviders:Tripo:EnabledForNewJobs"], "false", StringComparison.OrdinalIgnoreCase);
            var isConfigured = !string.IsNullOrWhiteSpace(_tripoOptions.Value.ApiKey);
            return new ProviderCapabilities(
                ProviderKey: "Tripo",
                ModelKey: modelKey ?? _tripoOptions.Value.DefaultEngine ?? "v3.1-20260211",
                ApiVersion: "v3",
                PricingVersion: "2026.01",
                AccountScope: "default",
                SupportsImageTo3D: true,
                SupportsTextTo3D: false,
                SupportsTexture: true,
                SupportsResize: false,
                SupportsConvert: true,
                SupportsAnalyze: true,
                MinHeightMm: 100,
                MaxHeightMm: 120,
                AllowedFormats: ["glb", "stl"],
                IsEnabledForNewJobs: !isExplicitlyDisabled && isConfigured
            );
        }

        if (string.Equals(providerKey, "Hunyuan3D", StringComparison.OrdinalIgnoreCase)
            || string.Equals(providerKey, "Hunyuan", StringComparison.OrdinalIgnoreCase)
            || string.Equals(providerKey, "Tencent", StringComparison.OrdinalIgnoreCase)
            || string.Equals(providerKey, "hy3d", StringComparison.OrdinalIgnoreCase)
            || string.Equals(providerKey, _hunyuanOptions.Value.DefaultEngine, StringComparison.OrdinalIgnoreCase))
        {
            var isExplicitlyDisabled = string.Equals(_configuration["AiProviders:Hunyuan3D:EnabledForNewJobs"], "false", StringComparison.OrdinalIgnoreCase);
            var isConfigured = !string.IsNullOrWhiteSpace(_hunyuanOptions.Value.SecretId) && !string.IsNullOrWhiteSpace(_hunyuanOptions.Value.SecretKey);
            return new ProviderCapabilities(
                ProviderKey: "Hunyuan3D",
                ModelKey: modelKey ?? _hunyuanOptions.Value.DefaultEngine ?? "pro",
                ApiVersion: _hunyuanOptions.Value.Version ?? "2023-09-01",
                PricingVersion: "2026.01",
                AccountScope: "default",
                SupportsImageTo3D: true,
                SupportsTextTo3D: false,
                SupportsTexture: true,
                SupportsResize: false,
                SupportsConvert: true,
                SupportsAnalyze: true,
                MinHeightMm: 100,
                MaxHeightMm: 120,
                AllowedFormats: ["glb", "stl"],
                IsEnabledForNewJobs: !isExplicitlyDisabled && isConfigured
            );
        }

        throw new ProviderException($"Không nhận diện được provider '{providerKey}'.");
    }

    public IReadOnlyList<string> GetAvailableProviderKeys() => _providers.Keys.ToList();
}
