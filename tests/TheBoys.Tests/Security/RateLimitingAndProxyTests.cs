using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TheBoys.Application.Printing;
using TheBoys.Infrastructure.Persistence;
using TheBoys.Infrastructure.Printing;
using TheBoys.WebApi;

namespace TheBoys.Tests;

public class RateLimitingAndProxyTests
{
    private static WebApplicationFactory<Program> CreateIsolatedFactory(Action<IConfigurationBuilder>? configureConfig = null)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:InitializeOnStartup"] = "false",
                    ["BootstrapAdmin:Email"] = "",
                    ["BootstrapAdmin:Password"] = "",
                    ["JwtSettings:Secret"] = "test-only-secret-not-for-real-use-0123456789"
                });
                configureConfig?.Invoke(config);
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IModelGenerationProvider>();
                services.AddSingleton<IModelGenerationProvider>(new FakeProvider());
                services.RemoveAll<IModelStorage>();
                services.AddSingleton<IModelStorage>(new TestStorage());
            });
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
        var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.NotNull(content);
        Assert.Equal("ok", content["status"]);
    }
}
