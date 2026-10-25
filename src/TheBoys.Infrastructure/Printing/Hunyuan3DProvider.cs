using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TheBoys.Application.Printing;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;
using GenerationStage = TheBoys.Domain.Enums.GenerationStage;

namespace TheBoys.Infrastructure.Printing;

public sealed class Hunyuan3DProvider(
    HttpClient http,
    IModelStorage storage,
    IOptions<Hunyuan3DOptions> options) : IModelGenerationProvider
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.Value.SecretId) && !string.IsNullOrWhiteSpace(options.Value.SecretKey);
    public string Name => "Hunyuan3D";
    public string Model => options.Value.DefaultEngine;
    public bool RequiresResizeStage => false;

    public static (HttpRequestMessage Request, string Authorization, string StringToSign, string CanonicalRequest) SignTc3Request(
        string baseUrl,
        string action,
        string service,
        string version,
        string region,
        string secretId,
        string secretKey,
        string jsonPayload,
        long timestamp)
    {
        var uri = new Uri(baseUrl);
        var host = uri.Host;
        var date = DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime.ToString("yyyy-MM-dd");
        var contentType = "application/json; charset=utf-8";
        var actionLower = action.ToLowerInvariant();
        var canonicalHeaders = $"content-type:{contentType}\nhost:{host}\nx-tc-action:{actionLower}\n";
        var signedHeaders = "content-type;host;x-tc-action";
        var hashedPayload = HexSha256(jsonPayload);
        var canonicalRequest = $"POST\n/\n\n{canonicalHeaders}\n{signedHeaders}\n{hashedPayload}";
        var hashedCanonicalRequest = HexSha256(canonicalRequest);
        var credentialScope = $"{date}/{service}/tc3_request";
        var stringToSign = $"TC3-HMAC-SHA256\n{timestamp}\n{credentialScope}\n{hashedCanonicalRequest}";

        var secretDate = HmacSha256(Encoding.UTF8.GetBytes("TC3" + secretKey), Encoding.UTF8.GetBytes(date));
        var secretService = HmacSha256(secretDate, Encoding.UTF8.GetBytes(service));
        var secretSigning = HmacSha256(secretService, Encoding.UTF8.GetBytes("tc3_request"));
        var signatureBytes = HmacSha256(secretSigning, Encoding.UTF8.GetBytes(stringToSign));
        var signature = Convert.ToHexString(signatureBytes).ToLowerInvariant();

        var authorization = $"TC3-HMAC-SHA256 Credential={secretId}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}";

        var request = new HttpRequestMessage(HttpMethod.Post, baseUrl);
        request.Headers.TryAddWithoutValidation("Authorization", authorization);
        request.Headers.TryAddWithoutValidation("Host", host);
        request.Headers.TryAddWithoutValidation("X-TC-Action", action);
        request.Headers.TryAddWithoutValidation("X-TC-Timestamp", timestamp.ToString(CultureInfo.InvariantCulture));
        request.Headers.TryAddWithoutValidation("X-TC-Version", version);
        if (!string.IsNullOrWhiteSpace(region))
        {
            request.Headers.TryAddWithoutValidation("X-TC-Region", region);
        }
        request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        return (request, authorization, stringToSign, canonicalRequest);
    }

    private static string HexSha256(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static byte[] HmacSha256(byte[] key, byte[] data)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(data);
    }

    private HttpRequestMessage BuildRequest(string action, string jsonPayload)
    {
        if (!IsConfigured)
            throw new ProviderException("Chưa cấu hình SecretId/SecretKey cho Tencent Hunyuan3D.");

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var (request, _, _, _) = SignTc3Request(
            baseUrl: options.Value.BaseUrl,
            action: action,
            service: options.Value.Service,
            version: options.Value.Version,
            region: options.Value.Region,
            secretId: options.Value.SecretId,
            secretKey: options.Value.SecretKey,
            jsonPayload: jsonPayload,
            timestamp: timestamp);

        return request;
    }

    public async Task<string> CreateAsync(GenerationTask task, CancellationToken ct)
    {
        return task.Stage switch
        {
            GenerationStage.Generate => await GenerateAsync(task, ct),
            GenerationStage.Convert => $"convert:{task.Id:N}:{task.ProviderTaskId}",
            GenerationStage.Analyze => $"local-{task.Id:N}-{task.RequestedHeightMm}",
            GenerationStage.Resize => throw new ProviderException("Hunyuan3D không dùng bước resize riêng."),
            _ => throw new ArgumentOutOfRangeException(nameof(task))
        };
    }

    private async Task<string> GenerateAsync(GenerationTask task, CancellationToken ct)
    {
        var engine = !string.IsNullOrWhiteSpace(task.AiModel) ? task.AiModel : options.Value.DefaultEngine;
        var isRapid = engine.Contains("rapid", StringComparison.OrdinalIgnoreCase) || engine.Contains("express", StringComparison.OrdinalIgnoreCase);
        var action = isRapid ? "SubmitHunyuanTo3DRapidJob" : "SubmitHunyuanTo3DProJob";

        var dataUri = await storage.ReadImageDataUriAsync(task.InputImageUrl!, ct);
        var commaIdx = dataUri.IndexOf(',');
        var base64 = commaIdx >= 0 ? dataUri[(commaIdx + 1)..] : dataUri;

        var payload = new Dictionary<string, object?>
        {
            ["ImageBase64"] = base64,
            ["ResultFormat"] = "GLB"
        };

        if (task.InputImageUrl != null && (task.InputImageUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || task.InputImageUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
        {
            payload["ImageUrl"] = task.InputImageUrl;
        }

        var jsonPayload = JsonSerializer.Serialize(payload);
        using var request = BuildRequest(action, jsonPayload);
        var responseElem = await SendForResponseAsync(request, creating: true, ct);

        string? jobId = null;
        if (responseElem.TryGetProperty("JobId", out var jProp) && jProp.ValueKind == JsonValueKind.String)
        {
            jobId = jProp.GetString();
        }
        else if (responseElem.TryGetProperty("TaskId", out var tProp) && tProp.ValueKind == JsonValueKind.String)
        {
            jobId = tProp.GetString();
        }

        if (string.IsNullOrWhiteSpace(jobId) || jobId.Length > 150)
        {
            throw new ProviderException("Hunyuan3D có thể đã nhận yêu cầu nhưng chưa lưu được task ID. Staff cần đối soát trước khi thử lại.", uncertain: true);
        }

        return jobId;
    }

    public async Task<ProviderTask> GetAsync(GenerationStage stage, string taskId, CancellationToken ct)
    {
        return stage switch
        {
            GenerationStage.Generate => await QueryGenerateAsync(taskId, ct),
            GenerationStage.Convert => await ConvertLocalAsync(taskId, ct),
            GenerationStage.Analyze => AnalyzeLocal(taskId),
            GenerationStage.Resize => throw new ProviderException("Hunyuan3D không dùng bước resize riêng."),
            _ => throw new ArgumentOutOfRangeException(nameof(stage))
        };
    }

    private async Task<ProviderTask> QueryGenerateAsync(string taskId, CancellationToken ct)
    {
        var isRapid = options.Value.DefaultEngine.Contains("rapid", StringComparison.OrdinalIgnoreCase) || options.Value.DefaultEngine.Contains("express", StringComparison.OrdinalIgnoreCase);
        var action = isRapid ? "QueryHunyuanTo3DRapidJob" : "QueryHunyuanTo3DProJob";

        var jsonPayload = JsonSerializer.Serialize(new { JobId = taskId });
        using var request = BuildRequest(action, jsonPayload);
        var responseElem = await SendForResponseAsync(request, creating: false, ct);

        try
        {
            var rawStatus = responseElem.TryGetProperty("Status", out var statusProp) && statusProp.ValueKind == JsonValueKind.String
                ? statusProp.GetString()! : throw new JsonException("Missing Status in Hunyuan3D response");

            var status = rawStatus switch
            {
                "WAIT" => "PENDING",
                "RUN" => "IN_PROGRESS",
                "DONE" => "SUCCEEDED",
                "FAIL" => "FAILED",
                _ => throw new JsonException($"Unknown Hunyuan3D status: {rawStatus}")
            };

            var progress = 0;
            if (responseElem.TryGetProperty("Progress", out var progProp) && progProp.TryGetInt32(out var pVal))
            {
                progress = pVal;
            }
            else
            {
                progress = status switch
                {
                    "SUCCEEDED" => 100,
                    "IN_PROGRESS" => 50,
                    _ => 10
                };
            }

            var urls = new Dictionary<string, string>();
            if (responseElem.TryGetProperty("ResultFile3Ds", out var filesProp) && filesProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var fileElem in filesProp.EnumerateArray())
                {
                    var format = (fileElem.TryGetProperty("Type", out var typeProp) ? typeProp.GetString() : null)
                        ?? (fileElem.TryGetProperty("Format", out var fmt) ? fmt.GetString() : null);
                    format = format?.ToLowerInvariant();
                    var url = fileElem.TryGetProperty("Url", out var u) ? u.GetString() : null;
                    if (!string.IsNullOrWhiteSpace(format) && !string.IsNullOrWhiteSpace(url))
                    {
                        urls[format] = url;
                    }
                }
            }

            string? thumbnail = null;
            if (responseElem.TryGetProperty("Render3DImageUrl", out var renderImg) && renderImg.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(renderImg.GetString()))
            {
                thumbnail = renderImg.GetString();
            }
            else if (responseElem.TryGetProperty("PreviewImageUrl", out var prevImg) && prevImg.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(prevImg.GetString()))
            {
                thumbnail = prevImg.GetString();
            }

            string? error = null;
            if (status == "FAILED")
            {
                if (responseElem.TryGetProperty("ErrorMessage", out var errProp) && errProp.ValueKind == JsonValueKind.String)
                    error = errProp.GetString();
                else if (responseElem.TryGetProperty("StatusStr", out var statusStrProp) && statusStrProp.ValueKind == JsonValueKind.String)
                    error = statusStrProp.GetString();
                else
                    error = "Hunyuan3D xử lý thất bại.";
            }

            decimal? credits = null;
            if (responseElem.TryGetProperty("Credits", out var credElem) && credElem.TryGetDecimal(out var cVal))
                credits = cVal;
            else if (responseElem.TryGetProperty("Cost", out var costElem) && costElem.TryGetDecimal(out var costVal))
                credits = costVal;

            return new ProviderTask(
                status,
                progress,
                urls,
                null,
                thumbnail,
                error,
                Math.Clamp(options.Value.PollIntervalSeconds, 2, 60),
                credits);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            throw new ProviderException("Chưa đọc được trạng thái Hunyuan3D; sẽ kiểm tra lại.", transient: true);
        }
    }

    private async Task<ProviderTask> ConvertLocalAsync(string taskId, CancellationToken ct)
    {
        var parts = taskId.Split(':', 3);
        if (parts.Length < 3 || parts[0] != "convert" || !Guid.TryParseExact(parts[1], "N", out var id))
            throw new ProviderException("Task convert STL cục bộ không hợp lệ.");

        var stlKey = storage.ModelKey(id, "stl");
        var stlPath = storage.ResolvePath(stlKey);

        if (!File.Exists(stlPath) || new FileInfo(stlPath).Length < 84)
        {
            var glbKey = storage.ModelKey(id, "glb");
            var glbPath = storage.ResolvePath(glbKey);
            if (!File.Exists(glbPath))
                throw new ProviderException("Không tìm thấy file GLB để chuyển đổi sang STL.");

            var glbBytes = await File.ReadAllBytesAsync(glbPath, ct);
            var stlBytes = GlbToStlConverter.ConvertGlbToStl(glbBytes);
            Directory.CreateDirectory(Path.GetDirectoryName(stlPath)!);
            await File.WriteAllBytesAsync(stlPath, stlBytes, ct);
        }

        return new ProviderTask(
            "SUCCEEDED",
            100,
            new Dictionary<string, string> { ["stl"] = $"local://stl/{id:N}" },
            null,
            null,
            null,
            2);
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
            throw new ProviderException("Không phân tích được STL do Hunyuan3D tạo ra.");
        }
    }

    private async Task<JsonElement> SendForResponseAsync(HttpRequestMessage request, bool creating, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            throw new ProviderException(creating
                ? "Mất kết nối khi gửi Hunyuan3D; cần đối soát task, không tự tạo lại."
                : "Mất kết nối Hunyuan3D; sẽ kiểm tra lại.",
                uncertain: creating,
                transient: !creating);
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                var retryAfter = response.Headers.RetryAfter?.Delta?.TotalSeconds ??
                    (response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow)?.TotalSeconds ?? options.Value.PollIntervalSeconds;

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    throw new ProviderException($"Hunyuan3D đang giới hạn tần suất yêu cầu (HTTP 429). Cần đợi {(int)Math.Clamp(retryAfter, 2, 300)}s.",
                        uncertain: false,
                        transient: true);
                }

                throw new ProviderException($"Hunyuan3D trả HTTP {(int)response.StatusCode}. Kiểm tra SecretId, SecretKey hoặc số dư.",
                    uncertain: creating && (int)response.StatusCode >= 500,
                    transient: !creating && (response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500));
            }

            try
            {
                using var json = JsonDocument.Parse(body);
                var root = json.RootElement;
                if (!root.TryGetProperty("Response", out var respElem))
                {
                    throw new JsonException("Thiếu thuộc tính Response trong phản hồi Tencent Cloud.");
                }

                if (respElem.TryGetProperty("Error", out var errElem))
                {
                    var code = errElem.TryGetProperty("Code", out var codeElem) ? codeElem.GetString() : "UnknownError";
                    var message = errElem.TryGetProperty("Message", out var msgElem) ? msgElem.GetString() : "Lỗi Tencent Cloud API";
                    throw new ProviderException($"Hunyuan3D từ chối yêu cầu ({code}): {message}", uncertain: false, transient: false);
                }

                return respElem.Clone();
            }
            catch (ProviderException)
            {
                throw;
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException)
            {
                throw new ProviderException(creating
                    ? "Hunyuan3D có thể đã nhận yêu cầu nhưng phản hồi không hợp lệ; cần đối soát trước khi thử lại."
                    : "Phản hồi Hunyuan3D không hợp lệ; sẽ kiểm tra lại.",
                    uncertain: creating,
                    transient: !creating);
            }
        }
    }
}
