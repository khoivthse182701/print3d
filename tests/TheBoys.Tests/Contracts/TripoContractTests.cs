using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TheBoys.Application.Printing;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;
using TheBoys.Infrastructure.Printing;

namespace TheBoys.Tests;

public class TripoContractTests
{
    [Fact]
    public async Task UploadGenerateAndPollUseV3Contract()
    {
        var requests = new List<(HttpMethod Method, string Path, string? Body)>();
        using var http = new HttpClient(new Handler(async request =>
        {
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("test-key", request.Headers.Authorization?.Parameter);
            var body = request.Content == null ? null : await request.Content.ReadAsStringAsync();
            requests.Add((request.Method, request.RequestUri!.AbsolutePath, body));
            return request.RequestUri.AbsolutePath switch
            {
                "/v3/files" => Json("{\"code\":0,\"data\":{\"file_token\":\"file_demo\"}}"),
                "/v3/generation/image-to-model" => Json("{\"code\":0,\"data\":{\"task_id\":\"task_generate\"}}"),
                "/v3/tasks/task_generate" => Json("{\"code\":0,\"data\":{\"status\":\"success\",\"progress\":100,\"output\":{\"model_url\":\"https://cdn.tripo3d.ai/model.glb\"}}}"),
                _ => throw new InvalidOperationException(request.RequestUri.AbsolutePath)
            };
        })) { BaseAddress = new Uri("https://openapi.tripo3d.ai/v3/") };
        var provider = Provider(http, new TestStorage());
        var task = new GenerationTask { Stage = GenerationStage.Generate, InputImageUrl = "input.png", ShouldTexture = false };

        Assert.Equal("task_generate", await provider.CreateAsync(task, default));
        var result = await provider.GetAsync(GenerationStage.Generate, "task_generate", default);

        Assert.False(provider.RequiresResizeStage);
        Assert.Equal("Tripo", provider.Name);
        Assert.Equal("SUCCEEDED", result.Status);
        Assert.Equal("https://cdn.tripo3d.ai/model.glb", result.ModelUrls["glb"]);
        Assert.Equal(new[] { "/v3/files", "/v3/generation/image-to-model", "/v3/tasks/task_generate" }, requests.Select(x => x.Path));
        var generate = JsonDocument.Parse(requests[1].Body!).RootElement;
        Assert.Equal("file_demo", generate.GetProperty("input").GetString());
        Assert.Equal("v3.1-20260211", generate.GetProperty("model").GetString());
        Assert.False(generate.GetProperty("texture").GetBoolean());
    }

    [Fact]
    public async Task ConvertUsesOriginalGenerationTaskAndRequestsStl()
    {
        JsonElement body = default;
        using var http = new HttpClient(new Handler(async request =>
        {
            Assert.Equal("/v3/models/convert", request.RequestUri!.AbsolutePath);
            body = JsonSerializer.Deserialize<JsonElement>(await request.Content!.ReadAsStringAsync());
            return Json("{\"code\":0,\"data\":{\"task_id\":\"task_convert\"}}");
        })) { BaseAddress = new Uri("https://openapi.tripo3d.ai/v3/") };
        var provider = Provider(http, new TestStorage());

        var id = await provider.CreateAsync(new GenerationTask
        {
            Stage = GenerationStage.Convert,
            ProviderTaskId = "task_generate"
        }, default);

        Assert.Equal("task_convert", id);
        Assert.Equal("task_generate", body.GetProperty("input").GetString());
        Assert.Equal("STL", body.GetProperty("format").GetString());
        Assert.True(body.GetProperty("flatten_bottom").GetBoolean());
    }

    [Fact]
    public async Task LocalAnalysisReturnsRequestedHeightWithoutCallingTripo()
    {
        var storage = new TestStorage();
        try
        {
            var id = Guid.NewGuid();
            var key = await storage.DownloadAsync(id, "stl", "unused", default);
            using var http = new HttpClient(new Handler(_ => throw new InvalidOperationException("No HTTP expected")))
                { BaseAddress = new Uri("https://openapi.tripo3d.ai/v3/") };
            var provider = Provider(http, storage);
            var task = new GenerationTask { Id = id, Stage = GenerationStage.Analyze, RequestedHeightMm = 110, StoragePathStl = key };

            var localId = await provider.CreateAsync(task, default);
            var result = await provider.GetAsync(GenerationStage.Analyze, localId, default);

            Assert.Equal("SUCCEEDED", result.Status);
            Assert.Equal(110, result.Printability!.Value.GetProperty("intended_print").GetProperty("dimensions_mm")[2].GetInt32());
        }
        finally { storage.Cleanup(); }
    }

    private static TripoProvider Provider(HttpClient http, IModelStorage storage) => new(http, storage,
        Options.Create(new TripoOptions { ApiKey = "test-key", DefaultEngine = "v3.1-20260211" }));
    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
        { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request);
    }
}
