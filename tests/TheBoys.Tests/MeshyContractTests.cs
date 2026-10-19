using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TheBoys.Application.Printing;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;
using TheBoys.Infrastructure.Printing;

namespace TheBoys.Tests;

public class MeshyContractTests
{
    [Fact]
    public async Task FourStagesSendDocumentedPayloadsAndResizeInMeters()
    {
        var requests = new List<(string Path, JsonElement Body)>();
        using var http = new HttpClient(new Handler(async request =>
        {
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("test-key", request.Headers.Authorization?.Parameter);
            requests.Add((request.RequestUri!.AbsolutePath, JsonSerializer.Deserialize<JsonElement>(await request.Content!.ReadAsStringAsync())));
            return Json(HttpStatusCode.OK, "{\"result\":\"provider-id\"}");
        })) { BaseAddress = new Uri("https://api.meshy.ai/") };
        var provider = Provider(http);
        var task = new GenerationTask { InputImageUrl = "input.png", RequestedHeightMm = 110,
            ProviderTaskId = "original", ResizeTaskId = "resized", ResizedModelUrl = "https://assets.meshy.ai/resized.glb" };
        foreach (var stage in new[] { GenerationStage.Generate, GenerationStage.Resize, GenerationStage.Convert, GenerationStage.Analyze })
        { task.Stage = stage; Assert.Equal("provider-id", await provider.CreateAsync(task, default)); }
        Assert.Equal("/openapi/v1/image-to-3d", requests[0].Path);
        Assert.StartsWith("data:image/png;base64,", requests[0].Body.GetProperty("image_url").GetString());
        Assert.Equal("/openapi/v1/resize", requests[1].Path);
        Assert.Equal(0.11m, requests[1].Body.GetProperty("resize_height").GetDecimal());
        Assert.Equal("resized", requests[2].Body.GetProperty("input_task_id").GetString());
        Assert.Equal("stl", requests[2].Body.GetProperty("target_formats")[0].GetString());
        Assert.Equal("/openapi/v1/print/analyze", requests[3].Path);
        Assert.Equal(110, requests[3].Body.GetProperty("intended_print_height_mm").GetInt32());
        Assert.Equal("https://assets.meshy.ai/resized.glb", requests[3].Body.GetProperty("model_url").GetString());
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, "{}")]
    [InlineData(HttpStatusCode.OK, "{}")]
    public async Task AmbiguousCreateIsNotRetryable(HttpStatusCode status, string body)
    {
        using var http = new HttpClient(new Handler(_ => Task.FromResult(Json(status, body)))) { BaseAddress = new Uri("https://api.meshy.ai/") };
        var error = await Assert.ThrowsAsync<ProviderException>(() => Provider(http).CreateAsync(new GenerationTask { Stage = GenerationStage.Resize }, default));
        Assert.True(error.Uncertain); Assert.False(error.Transient);
    }

    [Fact]
    public async Task Poll429IsRetryableAndPreservesTheExistingTask()
    {
        using var http = new HttpClient(new Handler(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("/openapi/v1/image-to-3d/existing", request.RequestUri!.AbsolutePath);
            return Task.FromResult(Json(HttpStatusCode.TooManyRequests, "{}"));
        })) { BaseAddress = new Uri("https://api.meshy.ai/") };
        var error = await Assert.ThrowsAsync<ProviderException>(() => Provider(http).GetAsync(GenerationStage.Generate, "existing", default));
        Assert.True(error.Transient); Assert.False(error.Uncertain);
    }

    private static MeshyProvider Provider(HttpClient http) => new(http, new TestStorage(), Options.Create(new MeshyOptions { ApiKey = "test-key" }));
    private static HttpResponseMessage Json(HttpStatusCode status, string body) => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request);
    }
}
