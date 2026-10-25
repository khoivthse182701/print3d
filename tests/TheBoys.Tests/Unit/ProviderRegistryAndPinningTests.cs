using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using TheBoys.Application.Printing;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;
using GenerationStage = TheBoys.Domain.Enums.GenerationStage;
using TheBoys.Infrastructure.Printing;

namespace TheBoys.Tests.Unit;

public class ProviderRegistryAndPinningTests
{
    [Fact]
    public void RegistryResolvesActiveProviderForNewJob()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AiProviders:Active"] = "Meshy"
        }).Build();

        var registry = CreateRegistry(config);

        var provider = registry.ResolveForNewJob(requestedCapability: "ImageTo3D");
        Assert.Equal("Meshy", provider.Name);
    }

    [Fact]
    public void RegistryResolvesHunyuan3DWhenActive()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AiProviders:Active"] = "Hunyuan3D"
        }).Build();

        var registry = CreateRegistry(config);

        var provider = registry.ResolveForNewJob(requestedCapability: "ImageTo3D");
        Assert.Equal("Hunyuan3D", provider.Name);
    }

    [Fact]
    public void RegistryResolvesHunyuanAliasesCorrectly()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AiProviders:Active"] = "Hunyuan3D"
        }).Build();

        var registry = CreateRegistry(config);

        Assert.Equal("Hunyuan3D", registry.Resolve("Hunyuan3D").Name);
        Assert.Equal("Hunyuan3D", registry.Resolve("Hunyuan").Name);
        Assert.Equal("Hunyuan3D", registry.Resolve("Tencent").Name);
        Assert.Equal("Hunyuan3D", registry.Resolve("hy3d").Name);
        Assert.Equal("Hunyuan3D", registry.Resolve("pro").Name);
    }

    [Fact]
    public void HistoricalJobPinnedToMeshyStillResolvesMeshyWhenDefaultChangesToTripo()
    {
        // Default is changed to Tripo
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AiProviders:Active"] = "Tripo"
        }).Build();

        var registry = CreateRegistry(config);

        // Historical job has pinned Provider = "Meshy"
        var historicalTask = new GenerationTask { Provider = "Meshy", AiModel = "meshy-6-lite" };

        var resolvedProvider = registry.Resolve(historicalTask.Provider);
        Assert.Equal("Meshy", resolvedProvider.Name);
    }

    [Fact]
    public void HistoricalJobPinnedToTripoStillResolvesTripoWhenDefaultIsMeshy()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AiProviders:Active"] = "Meshy"
        }).Build();

        var registry = CreateRegistry(config);

        var historicalTask = new GenerationTask { Provider = "Tripo", AiModel = "v3.1-20260211" };

        var resolvedProvider = registry.Resolve(historicalTask.Provider);
        Assert.Equal("Tripo", resolvedProvider.Name);
    }

    [Fact]
    public void HistoricalJobPinnedToHunyuanStillResolvesHunyuanWhenDefaultIsMeshy()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AiProviders:Active"] = "Meshy"
        }).Build();

        var registry = CreateRegistry(config);

        var historicalTask = new GenerationTask { Provider = "Hunyuan3D", AiModel = "pro" };

        var resolvedProvider = registry.Resolve(historicalTask.Provider);
        Assert.Equal("Hunyuan3D", resolvedProvider.Name);
    }

    [Fact]
    public void DisabledProviderRejectsNewJobsButHistoricalJobResolvesIfConfigured()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AiProviders:Active"] = "Meshy",
            ["AiProviders:Meshy:EnabledForNewJobs"] = "false"
        }).Build();

        var registry = CreateRegistry(config);

        // Reject new job when provider is disabled
        var ex = Assert.Throws<ProviderException>(() => registry.ResolveForNewJob());
        Assert.Contains("vô hiệu hóa", ex.Message);

        // But historical job pinned to Meshy can still be resolved because adapter credentials exist
        var historicalProvider = registry.Resolve("Meshy");
        Assert.Equal("Meshy", historicalProvider.Name);
    }

    [Fact]
    public void UnknownProviderKeyDoesNotFallbackSilently()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AiProviders:Active"] = "Meshy"
        }).Build();

        var registry = CreateRegistry(config);

        var ex = Assert.Throws<ProviderException>(() => registry.Resolve("UnknownNonExistent3D"));
        Assert.Contains("UnknownNonExistent3D", ex.Message);
        Assert.Contains("không được hỗ trợ", ex.Message);
    }

    [Fact]
    public void UnsupportedCapabilityThrowsBeforeCreatingJob()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AiProviders:Active"] = "Meshy"
        }).Build();

        var registry = CreateRegistry(config);

        var ex = Assert.Throws<ProviderException>(() => registry.ResolveForNewJob(requestedCapability: "TextTo3D"));
        Assert.Contains("không hỗ trợ", ex.Message);
    }

    private static ModelProviderRegistry CreateRegistry(
        IConfiguration config,
        string meshyKey = "test-meshy-key",
        string tripoKey = "test-tripo-key",
        string hunyuanSecretId = "test-hunyuan-id",
        string hunyuanSecretKey = "test-hunyuan-key")
    {
        var meshy = CreateMockMeshy(meshyKey);
        var tripo = CreateMockTripo(tripoKey);
        var hunyuan = CreateMockHunyuan(hunyuanSecretId, hunyuanSecretKey);
        return new ModelProviderRegistry(
            meshy,
            tripo,
            hunyuan,
            config,
            Options.Create(new MeshyOptions { ApiKey = meshyKey }),
            Options.Create(new TripoOptions { ApiKey = tripoKey }),
            Options.Create(new Hunyuan3DOptions { SecretId = hunyuanSecretId, SecretKey = hunyuanSecretKey }));
    }

    private static MeshyProvider CreateMockMeshy(string apiKey)
    {
        var http = new HttpClient(new StubHandler()) { BaseAddress = new Uri("https://api.meshy.ai/") };
        return new MeshyProvider(http, new TestStorage(), Options.Create(new MeshyOptions { ApiKey = apiKey }));
    }

    private static TripoProvider CreateMockTripo(string apiKey)
    {
        var http = new HttpClient(new StubHandler()) { BaseAddress = new Uri("https://openapi.tripo3d.ai/v3/") };
        return new TripoProvider(http, new TestStorage(), Options.Create(new TripoOptions { ApiKey = apiKey }));
    }

    private static Hunyuan3DProvider CreateMockHunyuan(string secretId, string secretKey)
    {
        var http = new HttpClient(new StubHandler()) { BaseAddress = new Uri("https://hunyuan.intl.tencentcloudapi.com/") };
        return new Hunyuan3DProvider(http, new TestStorage(), Options.Create(new Hunyuan3DOptions { SecretId = secretId, SecretKey = secretKey }));
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }
}
