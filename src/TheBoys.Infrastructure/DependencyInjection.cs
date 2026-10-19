using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Infrastructure.Persistence;
using TheBoys.Application.Printing;
using TheBoys.Infrastructure.Printing;

namespace TheBoys.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? "Server=(localdb)\\mssqllocaldb;Database=TheBoysDb;Trusted_Connection=True;MultipleActiveResultSets=true";

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IApplicationDbContext>(provider => 
            provider.GetRequiredService<ApplicationDbContext>());

        services.Configure<MeshyOptions>(configuration.GetSection("AiProviders:Meshy"));
        services.Configure<TripoOptions>(configuration.GetSection("AiProviders:Tripo"));
        services.Configure<PrintPricingOptions>(configuration.GetSection("PrintPricing"));
        services.AddSingleton<IModelStorage, LocalModelStorage>();
        var activeProvider = configuration["AiProviders:Active"] ?? "Meshy";
        if (string.Equals(activeProvider, "Tripo", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient<IModelGenerationProvider, TripoProvider>(client =>
            {
                var baseUrl = configuration["AiProviders:Tripo:BaseUrl"] ?? "https://openapi.tripo3d.ai/v3";
                if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !uri.IsDefaultPort
                    || !string.Equals(uri.Host, "openapi.tripo3d.ai", StringComparison.OrdinalIgnoreCase)
                    || uri.AbsolutePath.TrimEnd('/') != "/v3")
                    throw new InvalidOperationException("Tripo BaseUrl phải là https://openapi.tripo3d.ai/v3.");
                client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
                client.Timeout = TimeSpan.FromSeconds(60);
            }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        }
        else if (string.Equals(activeProvider, "Meshy", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient<IModelGenerationProvider, MeshyProvider>(client =>
            {
                var baseUrl = configuration["AiProviders:Meshy:BaseUrl"] ?? "https://api.meshy.ai";
                if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !uri.IsDefaultPort
                    || !string.Equals(uri.Host, "api.meshy.ai", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Meshy BaseUrl phải là https://api.meshy.ai.");
                client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
                client.Timeout = TimeSpan.FromSeconds(60);
            }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        }
        else throw new InvalidOperationException("AiProviders:Active chỉ nhận Meshy hoặc Tripo.");
        services.AddHttpClient("ModelAssets", client => client.Timeout = TimeSpan.FromMinutes(5))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddScoped<GenerationProcessor>();
        services.AddHostedService<GenerationWorker>();

        return services;
    }
}
