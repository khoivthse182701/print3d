using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TheBoys.Application.Printing;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;

namespace TheBoys.Infrastructure.Printing;

public sealed class MeshyProvider(HttpClient http, IModelStorage storage, IOptions<MeshyOptions> options)
    : IModelGenerationProvider
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.Value.ApiKey);
    public string Name => "Meshy";
    public string Model => options.Value.DefaultEngine;
    public bool RequiresResizeStage => true;

    public static string Endpoint(TheBoys.Domain.Enums.GenerationStage stage) => stage switch
    {
        TheBoys.Domain.Enums.GenerationStage.Generate => "image-to-3d",
        TheBoys.Domain.Enums.GenerationStage.Resize => "resize",
        TheBoys.Domain.Enums.GenerationStage.Convert => "convert",
        TheBoys.Domain.Enums.GenerationStage.Analyze => "print/analyze",
        _ => throw new ArgumentOutOfRangeException(nameof(stage))
    };

    public async Task<string> CreateAsync(GenerationTask task, CancellationToken ct)
    {
        object body = task.Stage switch
        {
            TheBoys.Domain.Enums.GenerationStage.Generate => new
            {
                image_url = await storage.ReadImageDataUriAsync(task.InputImageUrl!, ct),
                ai_model = task.AiModel,
                should_texture = task.ShouldTexture,
                target_formats = new[] { "glb" }
            },
            TheBoys.Domain.Enums.GenerationStage.Resize => new { input_task_id = task.ProviderTaskId, resize_height = task.RequestedHeightMm / 1000m, origin_at = "bottom" },
            TheBoys.Domain.Enums.GenerationStage.Convert => new { input_task_id = task.ResizeTaskId, target_formats = new[] { "stl" } },
            TheBoys.Domain.Enums.GenerationStage.Analyze => new { model_url = task.ResizedModelUrl, printer_type = task.PrinterType, intended_print_height_mm = task.RequestedHeightMm },
            _ => throw new ArgumentOutOfRangeException(nameof(task))
        };
        using var request = Request(HttpMethod.Post, Endpoint(task.Stage));
        request.Content = JsonContent.Create(body);
        using var response = await SendAsync(request, true, ct);
        try
        {
            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var id = json.RootElement.GetProperty("result").GetString();
            if (string.IsNullOrWhiteSpace(id) || id.Length > 150) throw new JsonException();
            return id;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException or IOException or OperationCanceledException)
        {
            throw new ProviderException("Meshy có thể đã nhận yêu cầu nhưng chưa lưu được task ID. Staff cần đối soát trước khi thử lại.", uncertain: true);
        }
    }

    public async Task<ProviderTask> GetAsync(TheBoys.Domain.Enums.GenerationStage stage, string taskId, CancellationToken ct)
    {
        using var request = Request(HttpMethod.Get, $"{Endpoint(stage)}/{Uri.EscapeDataString(taskId)}");
        using var response = await SendAsync(request, false, ct);
        try
        {
            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var root = json.RootElement;
            var status = root.GetProperty("status").GetString() ?? throw new JsonException();
            var urls = new Dictionary<string, string>();
            if (root.TryGetProperty("model_urls", out var models) && models.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in models.EnumerateObject())
                {
                    if (p.Value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(p.Value.GetString()))
                        urls[p.Name] = p.Value.GetString()!;
                }
            }
            JsonElement? analysis = root.TryGetProperty("printability", out var value) && value.ValueKind == JsonValueKind.Object ? value.Clone() : null;
            var retry = response.Headers.RetryAfter?.Delta?.TotalSeconds ??
                (response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow)?.TotalSeconds ?? options.Value.PollIntervalSeconds;

            decimal? credits = null;
            if (root.TryGetProperty("credits", out var cElem) && cElem.TryGetDecimal(out var cVal))
                credits = cVal;
            else if (root.TryGetProperty("cost", out var costElem) && costElem.TryGetDecimal(out var costVal))
                credits = costVal;

            return new(status, root.TryGetProperty("progress", out var p1) ? p1.GetInt32() : 0,
                urls, analysis, root.TryGetProperty("thumbnail_url", out var thumb) ? thumb.GetString() : null,
                status is "FAILED" or "CANCELED" ? "Meshy xử lý thất bại. Kiểm tra task trong Developer Platform." : null,
                (int)Math.Clamp(retry, 5, 300),
                credits);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException or IOException)
        {
            throw new ProviderException("Chưa đọc được trạng thái Meshy; sẽ kiểm tra lại.", transient: true);
        }
    }

    private HttpRequestMessage Request(HttpMethod method, string path)
    {
        if (!IsConfigured) throw new ProviderException("Chưa cấu hình API key Meshy.");
        var request = new HttpRequestMessage(method, $"openapi/v1/{path}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.ApiKey);
        return request;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, bool creating, CancellationToken ct)
    {
        HttpResponseMessage response;
        try { response = await http.SendAsync(request, ct); }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            throw new ProviderException(creating ? "Mất kết nối khi gửi Meshy; cần đối soát task, không tự tạo lại." : "Mất kết nối Meshy; sẽ kiểm tra lại.", creating, !creating);
        }
        if (response.IsSuccessStatusCode) return response;
        var code = response.StatusCode;
        response.Dispose();
        // Never blindly retry POST: 5xx/timeouts may have created a billable task.
        throw new ProviderException($"Meshy trả HTTP {(int)code}. Kiểm tra cấu hình, số dư hoặc task tại Developer Platform.",
            uncertain: creating && (int)code >= 500,
            transient: !creating && (code == HttpStatusCode.TooManyRequests || (int)code >= 500));
    }
}
