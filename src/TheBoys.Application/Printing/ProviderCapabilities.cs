namespace TheBoys.Application.Printing;

public sealed record ProviderCapabilities(
    string ProviderKey,
    string ModelKey,
    string ApiVersion,
    string PricingVersion,
    string AccountScope,
    bool SupportsImageTo3D,
    bool SupportsTextTo3D,
    bool SupportsTexture,
    bool SupportsResize,
    bool SupportsConvert,
    bool SupportsAnalyze,
    int MinHeightMm,
    int MaxHeightMm,
    IReadOnlyList<string> AllowedFormats,
    bool IsEnabledForNewJobs
);

