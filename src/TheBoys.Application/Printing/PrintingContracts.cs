using System.Text.Json;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;

namespace TheBoys.Application.Printing;

public sealed class MeshyOptions
{
    public string BaseUrl { get; set; } = "https://api.meshy.ai";
    public string ApiKey { get; set; } = "";
    public string DefaultEngine { get; set; } = "meshy-6-lite";
    public int PollIntervalSeconds { get; set; } = 5;
    public string[] AssetHosts { get; set; } = ["assets.meshy.ai"];
}

public sealed class TripoOptions
{
    public string BaseUrl { get; set; } = "https://openapi.tripo3d.ai/v3";
    public string ApiKey { get; set; } = "";
    public string DefaultEngine { get; set; } = "v3.1-20260211";
    public int PollIntervalSeconds { get; set; } = 5;
    public string[] AssetHosts { get; set; } = ["cdn.tripo3d.ai", "*.data.tripo3d.com"];
}

public sealed class PrintPricingOptions
{
    public decimal DemoUnitPriceVnd { get; set; } = 390000;
    public decimal ShippingFeeVnd { get; set; } = 0;
    public int MaxQuantity { get; set; } = 10;
}

public sealed record PrintQuote(Guid GenerationTaskId, Guid PricingPackageId, string Material,
    int RequestedHeightMm, decimal WidthMm, decimal DepthMm, decimal HeightMm,
    decimal? ModelVolumeCm3, int Quantity, decimal UnitPriceVnd, decimal SubtotalVnd,
    decimal ShippingFeeVnd, decimal TotalVnd, bool ApprovedForPrinting, string PricingBasis);

public sealed class PrintPricingService
{
    public PrintQuote Quote(GenerationTask task, PricingPackage package, int quantity, PrintPricingOptions options)
    {
        if (task.Status != GenerationStatus.Succeeded || task.BoundingBoxX_Mm is not > 0
            || task.BoundingBoxY_Mm is not > 0 || task.BoundingBoxZ_Mm is not > 0)
            throw new InvalidOperationException("Model chưa hoàn tất hoặc chưa có kích thước đo được.");
        if (!package.IsActive || task.RequestedHeightMm < package.MinHeightMm || task.RequestedHeightMm > package.MaxHeightMm)
            throw new ArgumentException("Chiều cao không thuộc gói in đã chọn.");
        if (quantity < 1 || quantity > options.MaxQuantity) throw new ArgumentException("Số lượng không hợp lệ.");
        if (package.PriceVnd <= 0 || options.ShippingFeeVnd < 0) throw new ArgumentException("Bảng giá không hợp lệ.");
        var subtotal = decimal.Round(package.PriceVnd * quantity, 0, MidpointRounding.AwayFromZero);
        return new(task.Id, package.Id, package.Material, task.RequestedHeightMm,
            task.BoundingBoxX_Mm.Value, task.BoundingBoxY_Mm.Value, task.BoundingBoxZ_Mm.Value,
            task.EstimatedVolumeCm3, quantity, package.PriceVnd, subtotal, options.ShippingFeeVnd,
            subtotal + options.ShippingFeeVnd, task.IsApprovedForPrinting,
            "Giá gói theo mẫu; thể tích chỉ tham khảo, không phải lượng nhựa sau slicing. Phí giao hàng demo lấy từ cấu hình máy chủ.");
    }
}

public sealed record ProviderTask(string Status, int Progress, IReadOnlyDictionary<string, string> ModelUrls,
    JsonElement? Printability, string? ThumbnailUrl, string? Error, int RetryAfterSeconds = 5);

public interface IModelGenerationProvider
{
    bool IsConfigured { get; }
    string Name => Model;
    string Model { get; }
    bool RequiresResizeStage => true;
    Task<string> CreateAsync(GenerationTask task, CancellationToken ct);
    Task<ProviderTask> GetAsync(GenerationStage stage, string taskId, CancellationToken ct);
}

public interface IModelStorage
{
    Task<string> SaveImageAsync(Guid id, byte[] data, string contentType, CancellationToken ct);
    Task<string> ReadImageDataUriAsync(string key, CancellationToken ct);
    Task<string> DownloadAsync(Guid id, string kind, string url, CancellationToken ct);
    string ModelKey(Guid id, string kind);
    string ResolvePath(string key);
}

public sealed class ProviderException(string message, bool uncertain = false, bool transient = false) : Exception(message)
{
    public bool Uncertain { get; } = uncertain;
    public bool Transient { get; } = transient;
}
