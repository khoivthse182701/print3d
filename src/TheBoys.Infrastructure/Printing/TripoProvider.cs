using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TheBoys.Application.Printing;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;

namespace TheBoys.Infrastructure.Printing;

public sealed class TripoProvider(HttpClient http, IModelStorage storage, IOptions<TripoOptions> options)
    : IModelGenerationProvider
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.Value.ApiKey);
    public string Name => "Tripo";
    public string Model => options.Value.DefaultEngine;
    public bool RequiresResizeStage => false;

    public async Task<string> CreateAsync(GenerationTask task, CancellationToken ct)
    {
        return task.Stage switch
        {
            GenerationStage.Generate => await GenerateAsync(task, ct),
            GenerationStage.Convert => await ConvertToStlAsync(task, ct),
            GenerationStage.Analyze => CreateLocalAnalysisId(task),
            GenerationStage.Resize => throw new ProviderException("Tripo không dùng bước resize riêng."),
            _ => throw new ArgumentOutOfRangeException(nameof(task))
        };
    }

    public async Task<ProviderTask> GetAsync(GenerationStage stage, string taskId, CancellationToken ct)
    {
        if (stage == GenerationStage.Analyze) return AnalyzeLocal(taskId);
        if (stage == GenerationStage.Resize) throw new ProviderException("Tripo không dùng bước resize riêng.");

        using var request = Request(HttpMethod.Get, $"tasks/{Uri.EscapeDataString(taskId)}");
        var data = await SendForDataAsync(request, creating: false, ct);
        try
        {
            var tripoStatus = data.GetProperty("status").GetString() ?? throw new JsonException();
            var status = tripoStatus switch
            {
                "queued" => "PENDING",
                "running" => "IN_PROGRESS",
                "success" => "SUCCEEDED",
                "failed" => "FAILED",
                "cancelled" or "banned" => "CANCELED",
                _ => throw new JsonException("Unknown Tripo status")
            };
            var urls = new Dictionary<string, string>();
            string? thumbnail = null;
            if (data.TryGetProperty("output", out var output) && output.ValueKind == JsonValueKind.Object)
            {
                if (output.TryGetProperty("model_url", out var modelUrl) && modelUrl.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(modelUrl.GetString()))
                    urls[stage == GenerationStage.Convert ? "stl" : "glb"] = modelUrl.GetString()!;
                if (output.TryGetProperty("rendered_image_url", out var rendered) && rendered.ValueKind == JsonValueKind.String)
                    thumbnail = rendered.GetString();
            }
            var error = data.TryGetProperty("error_message", out var errorMessage) && errorMessage.ValueKind == JsonValueKind.String
                ? errorMessage.GetString() : null;
            return new(status,
                data.TryGetProperty("progress", out var progress) && progress.TryGetInt32(out var value) ? value : 0,
                urls, null, thumbnail, error, Math.Clamp(options.Value.PollIntervalSeconds, 2, 60));
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            throw new ProviderException("Chưa đọc được trạng thái Tripo; sẽ kiểm tra lại.", transient: true);
        }
    }

    private async Task<string> GenerateAsync(GenerationTask task, CancellationToken ct)
    {
        var dataUri = await storage.ReadImageDataUriAsync(task.InputImageUrl!, ct);
        var separator = dataUri.IndexOf(',');
        if (separator < 0 || !dataUri.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
            throw new ProviderException("Ảnh upload không hợp lệ.");
        byte[] image;
        try { image = Convert.FromBase64String(dataUri[(separator + 1)..]); }
        catch (FormatException) { throw new ProviderException("Ảnh upload không hợp lệ."); }
        var mediaType = dataUri[5..dataUri.IndexOf(';')];
        var extension = mediaType switch { "image/png" => "png", "image/jpeg" => "jpg", "image/webp" => "webp", _ => "bin" };

        using var upload = Request(HttpMethod.Post, "files");
        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent(image);
        file.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        form.Add(file, "file", $"input.{extension}");
        upload.Content = form;
        var uploaded = await SendForDataAsync(upload, creating: false, ct);
        var token = uploaded.TryGetProperty("file_token", out var tokenElement) ? tokenElement.GetString() : null;
        if (string.IsNullOrWhiteSpace(token)) throw new ProviderException("Tripo không trả file token sau khi upload.");

        using var generate = Request(HttpMethod.Post, "generation/image-to-model");
        generate.Content = JsonContent.Create(new
        {
            input = token,
            model = options.Value.DefaultEngine,
            texture = task.ShouldTexture,
            pbr = task.ShouldTexture,
            auto_size = true,
            face_limit = 80000
        });
        var result = await SendForDataAsync(generate, creating: true, ct);
        return TaskId(result);
    }

    private async Task<string> ConvertToStlAsync(GenerationTask task, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(task.ProviderTaskId)) throw new ProviderException("Thiếu task tạo model Tripo.");
        using var request = Request(HttpMethod.Post, "models/convert");
        request.Content = JsonContent.Create(new
        {
            input = task.ProviderTaskId,
            format = "STL",
            flatten_bottom = true,
            pivot_to_center_bottom = true,
            with_animation = false
        });
        var result = await SendForDataAsync(request, creating: true, ct);
        return TaskId(result);
    }

    private static string TaskId(JsonElement data)
    {
        try
        {
            var id = data.GetProperty("task_id").GetString();
            if (string.IsNullOrWhiteSpace(id) || id.Length > 150) throw new JsonException();
            return id;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            throw new ProviderException("Tripo có thể đã nhận yêu cầu nhưng chưa lưu được task ID. Cần đối soát trước khi thử lại.", uncertain: true);
        }
    }

    private static string CreateLocalAnalysisId(GenerationTask task)
    {
        if (string.IsNullOrWhiteSpace(task.StoragePathStl)) throw new ProviderException("Chưa có STL để kiểm tra.");
        return $"local-{task.Id:N}-{task.RequestedHeightMm}";
    }

    private ProviderTask AnalyzeLocal(string taskId)
    {
        var parts = taskId.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3 || parts[0] != "local" || !Guid.TryParseExact(parts[1], "N", out var id)
            || !int.TryParse(parts[2], out var height) || height is < 1 or > 10000)
            throw new ProviderException("Task kiểm tra STL cục bộ không hợp lệ.");
        var path = storage.ResolvePath(storage.ModelKey(id, "stl"));
        try
        {
            var analysis = StlGeometry.AnalyzePrintability(path, height);
            return new("SUCCEEDED", 100, new Dictionary<string, string>(), analysis, null, null, 2);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            throw new ProviderException("Không phân tích được STL do Tripo trả về.");
        }
    }

    private HttpRequestMessage Request(HttpMethod method, string path)
    {
        if (!IsConfigured) throw new ProviderException("Chưa cấu hình API key Tripo.");
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.ApiKey);
        return request;
    }

    private async Task<JsonElement> SendForDataAsync(HttpRequestMessage request, bool creating, CancellationToken ct)
    {
        HttpResponseMessage response;
        try { response = await http.SendAsync(request, ct); }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            throw new ProviderException(creating
                ? "Mất kết nối khi gửi Tripo; cần đối soát task, không tự tạo lại."
                : "Mất kết nối Tripo; sẽ kiểm tra lại.", uncertain: creating, transient: !creating);
        }
        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
                throw new ProviderException($"Tripo trả HTTP {(int)response.StatusCode}. Kiểm tra API key, số dư hoặc task.",
                    uncertain: creating && (int)response.StatusCode >= 500,
                    transient: !creating && (response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500));
            try
            {
                using var json = JsonDocument.Parse(body);
                var root = json.RootElement;
                if (!root.TryGetProperty("code", out var code) || code.GetInt32() != 0)
                {
                    var providerCode = root.TryGetProperty("code", out code) ? code.GetInt32() : -1;
                    var message = root.TryGetProperty("message", out var value) && value.ValueKind == JsonValueKind.String
                        ? value.GetString() : "Yêu cầu bị từ chối";
                    throw new ProviderException($"Tripo từ chối yêu cầu ({providerCode}): {message}");
                }
                return root.GetProperty("data").Clone();
            }
            catch (ProviderException) { throw; }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException)
            {
                throw new ProviderException(creating
                    ? "Tripo có thể đã nhận yêu cầu nhưng phản hồi không hợp lệ; cần đối soát trước khi thử lại."
                    : "Phản hồi Tripo không hợp lệ; sẽ kiểm tra lại.", uncertain: creating, transient: !creating);
            }
        }
    }
}
