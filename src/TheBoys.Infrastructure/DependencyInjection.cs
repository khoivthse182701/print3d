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

        services.AddMemoryCache();
        services.AddSingleton<IUserRoleCache, Services.UserRoleCache>();

        services.Configure<MeshyOptions>(configuration.GetSection("AiProviders:Meshy"));
        services.Configure<PrintPricingOptions>(configuration.GetSection("PrintPricing"));
        services.AddSingleton<IModelStorage, LocalModelStorage>();
        services.AddHttpClient<IModelGenerationProvider, MeshyProvider>(client =>
        {
            var baseUrl = configuration["AiProviders:Meshy:BaseUrl"] ?? "https://api.meshy.ai";
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !uri.IsDefaultPort
                || !string.Equals(uri.Host, "api.meshy.ai", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Meshy BaseUrl phải là https://api.meshy.ai.");
            client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(60);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddHttpClient("MeshyAssets", client => client.Timeout = TimeSpan.FromMinutes(5))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddScoped<GenerationProcessor>();
        services.AddHostedService<GenerationWorker>();

        return services;
    }
}
