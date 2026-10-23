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

        var meshy = CreateMockMeshy("test-meshy-key");
        var tripo = CreateMockTripo("test-tripo-key");
        var registry = new ModelProviderRegistry(meshy, tripo, config,
            Options.Create(new MeshyOptions { ApiKey = "test-meshy-key" }),
            Options.Create(new TripoOptions { ApiKey = "test-tripo-key" }));

        var provider = registry.ResolveForNewJob(requestedCapability: "ImageTo3D");
        Assert.Equal("Meshy", provider.Name);
    }

    [Fact]
    public void HistoricalJobPinnedToMeshyStillResolvesMeshyWhenDefaultChangesToTripo()
    {
        // Default is changed to Tripo
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AiProviders:Active"] = "Tripo"
        }).Build();

        var meshy = CreateMockMeshy("test-meshy-key");
        var tripo = CreateMockTripo("test-tripo-key");
        var registry = new ModelProviderRegistry(meshy, tripo, config,
            Options.Create(new MeshyOptions { ApiKey = "test-meshy-key" }),
            Options.Create(new TripoOptions { ApiKey = "test-tripo-key" }));

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

        var meshy = CreateMockMeshy("test-meshy-key");
        var tripo = CreateMockTripo("test-tripo-key");
        var registry = new ModelProviderRegistry(meshy, tripo, config,
            Options.Create(new MeshyOptions { ApiKey = "test-meshy-key" }),
            Options.Create(new TripoOptions { ApiKey = "test-tripo-key" }));

        var historicalTask = new GenerationTask { Provider = "Tripo", AiModel = "v3.1-20260211" };

        var resolvedProvider = registry.Resolve(historicalTask.Provider);
        Assert.Equal("Tripo", resolvedProvider.Name);
    }

    [Fact]
    public void DisabledProviderRejectsNewJobsButHistoricalJobResolvesIfConfigured()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AiProviders:Active"] = "Meshy",
            ["AiProviders:Meshy:EnabledForNewJobs"] = "false"
        }).Build();

        var meshy = CreateMockMeshy("test-meshy-key");
        var tripo = CreateMockTripo("test-tripo-key");
        var registry = new ModelProviderRegistry(meshy, tripo, config,
            Options.Create(new MeshyOptions { ApiKey = "test-meshy-key" }),
            Options.Create(new TripoOptions { ApiKey = "test-tripo-key" }));

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

        var meshy = CreateMockMeshy("test-meshy-key");
        var tripo = CreateMockTripo("test-tripo-key");
        var registry = new ModelProviderRegistry(meshy, tripo, config,
            Options.Create(new MeshyOptions { ApiKey = "test-meshy-key" }),
            Options.Create(new TripoOptions { ApiKey = "test-tripo-key" }));

        var ex = Assert.Throws<ProviderException>(() => registry.Resolve("Tencent3D"));
        Assert.Contains("Tencent3D", ex.Message);
        Assert.Contains("không được hỗ trợ", ex.Message);
    }

    [Fact]
    public void UnsupportedCapabilityThrowsBeforeCreatingJob()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AiProviders:Active"] = "Meshy"
        }).Build();

        var meshy = CreateMockMeshy("test-meshy-key");
        var tripo = CreateMockTripo("test-tripo-key");
        var registry = new ModelProviderRegistry(meshy, tripo, config,
            Options.Create(new MeshyOptions { ApiKey = "test-meshy-key" }),
            Options.Create(new TripoOptions { ApiKey = "test-tripo-key" }));

        var ex = Assert.Throws<ProviderException>(() => registry.ResolveForNewJob(requestedCapability: "TextTo3D"));
        Assert.Contains("không hỗ trợ", ex.Message);
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

    private sealed class StubHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }
}

