using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TheBoys.Infrastructure.Persistence;
using TheBoys.WebApi;

namespace TheBoys.Tests;

public class RateLimitingAndProxyTests(DemoFactory factory) : IClassFixture<DemoFactory>
{
    private WebApplicationFactory<Program> CreateIsolatedFactory(
        Action<IConfigurationBuilder>? configureConfig = null,
        Action<IServiceCollection>? configureServices = null)
    {
        return factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RateLimitingSettings:AuthPermitLimit"] = "10",
                    ["AiProviders:Meshy:ApiKey"] = "",
                    ["AiProviders:Tripo:ApiKey"] = "",
                    ["AiProviders:Hunyuan:SecretId"] = "",
                    ["AiProviders:Hunyuan:SecretKey"] = "",
                    ["ModelStorage:BasePath"] = factory.Storage.ResolvePath("health")
                });
                configureConfig?.Invoke(config);
            });
            if (configureServices != null)
            {
                builder.ConfigureServices(configureServices);
            }
        });
    }

    [Fact]
    public async Task UntrustedClientSpoofedIpHeadersDoNotBypassRateLimiting()
    {
        await using var isolatedFactory = CreateIsolatedFactory();
        using var client = isolatedFactory.CreateClient();

        // Send 10 requests to exhaust the 10-permit limit for this client IP bucket
        var codes = new List<HttpStatusCode>();
        for (var i = 0; i < 10; i++)
        {
            var res = await client.PostAsJsonAsync("/api/auth/login", new { email = "nonexistent@test.com", password = "wrong-password-123" });
            codes.Add(res.StatusCode);
        }

        // 11th request: attempt to spoof client IP via CF-Connecting-IP, X-Real-IP, and X-Forwarded-For
        var spoofedRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { email = "nonexistent@test.com", password = "wrong-password-123" })
        };
        spoofedRequest.Headers.Add("CF-Connecting-IP", "203.0.113.195");
        spoofedRequest.Headers.Add("X-Real-IP", "203.0.113.195");
        spoofedRequest.Headers.Add("X-Forwarded-For", "203.0.113.195");

        var response = await client.SendAsync(spoofedRequest);
        // Untrusted proxy configuration MUST ignore client-supplied headers and enforce 429
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    [Fact]
    public async Task ForwardedHeadersAppliedWhenProxyIsConfiguredAsTrusted()
    {
        // Custom factory where 127.0.0.1 and loopback are explicitly trusted
        await using var trustedFactory = CreateIsolatedFactory(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ReverseProxySettings:KnownProxies:0"] = "127.0.0.1",
                ["ReverseProxySettings:KnownProxies:1"] = "::1",
                ["ReverseProxySettings:KnownNetworks:0"] = "127.0.0.0/8",
                ["ReverseProxySettings:ForwardLimit"] = "1"
            });
        });

        using var client = trustedFactory.CreateClient();

        // 10 requests from client IP A (forwarded through trusted proxy)
        for (var i = 0; i < 10; i++)
        {
            var reqA = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
            {
                Content = JsonContent.Create(new { email = "test@example.com", password = "wrong" })
            };
            reqA.Headers.Add("X-Forwarded-For", "198.51.100.10");
            reqA.Headers.Add("X-Forwarded-Proto", "https");
            var resA = await client.SendAsync(reqA);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, resA.StatusCode);
        }

        // 11th request from client IP A should be rate limited
        var reqA11 = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { email = "test@example.com", password = "wrong" })
        };
        reqA11.Headers.Add("X-Forwarded-For", "198.51.100.10");
        reqA11.Headers.Add("X-Forwarded-Proto", "https");
        var resA11 = await client.SendAsync(reqA11);
        Assert.Equal(HttpStatusCode.TooManyRequests, resA11.StatusCode);

        // A request from client IP B (different forwarded IP) should NOT be blocked by client IP A's quota
        var reqB = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { email = "test@example.com", password = "wrong" })
        };
        reqB.Headers.Add("X-Forwarded-For", "198.51.100.20");
        reqB.Headers.Add("X-Forwarded-Proto", "https");
        var resB = await client.SendAsync(reqB);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, resB.StatusCode);
    }

    [Fact]
    public async Task ApplicationBootsAndRespondsWithoutRealProviderSecrets()
    {
        await using var isolatedFactory = CreateIsolatedFactory();
        using var client = isolatedFactory.CreateClient();

        // Liveness probe returns 200 OK without external service dependencies
        var liveResponse = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, liveResponse.StatusCode);
        var liveContent = await liveResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("live", liveContent.GetProperty("status").GetString());

        // Readiness probe returns 200 OK because test dependencies (DB, storage) are provisioned,
        // and reports unconfigured AI provider keys truthfully without crashing.
        var readyResponse = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, readyResponse.StatusCode);

        var readyContent = await readyResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("ok", readyContent.GetProperty("status").GetString());
        Assert.Equal("ready", readyContent.GetProperty("readiness").GetString());
        Assert.True(readyContent.GetProperty("checks").GetProperty("database").GetProperty("healthy").GetBoolean());
        Assert.True(readyContent.GetProperty("checks").GetProperty("storage").GetProperty("healthy").GetBoolean());
        Assert.False(readyContent.GetProperty("checks").GetProperty("aiProviders").GetProperty("meshy").GetProperty("configured").GetBoolean());
        Assert.False(readyContent.GetProperty("checks").GetProperty("aiProviders").GetProperty("tripo").GetProperty("configured").GetBoolean());
    }

    [Fact]
    public async Task HealthEndpoint_WhenDatabaseUnreachable_ReturnsServiceUnavailable()
    {
        await using var unreachableDbFactory = CreateIsolatedFactory(configureServices: services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            var unreachableConn = "Server=(localdb)\\MSSQLLocalDB;Database=TheBoys_Unreachable_" + Guid.NewGuid().ToString("N") + ";Connect Timeout=1;Trusted_Connection=True;TrustServerCertificate=True";
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(unreachableConn));
        });
        using var client = unreachableDbFactory.CreateClient();

        // Liveness probe remains operational even when database is unreachable
        var liveResponse = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, liveResponse.StatusCode);

        // Readiness probe fails cleanly with 503 Service Unavailable
        var readyResponse = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, readyResponse.StatusCode);

        var readyContent = await readyResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("unhealthy", readyContent.GetProperty("status").GetString());
        Assert.Equal("unhealthy", readyContent.GetProperty("readiness").GetString());
        Assert.False(readyContent.GetProperty("checks").GetProperty("database").GetProperty("healthy").GetBoolean());
    }
}
