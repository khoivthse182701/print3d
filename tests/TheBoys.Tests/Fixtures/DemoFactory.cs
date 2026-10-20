using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TheBoys.Application.Printing;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;
using TheBoys.Infrastructure.Persistence;
using TheBoys.Infrastructure.Printing;
using TheBoys.WebApi;
using TheBoys.WebApi.Security;

namespace TheBoys.Tests;

public sealed class DemoFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string dbName = "TheBoys_Test_" + Guid.NewGuid().ToString("N");
    public FakeProvider Provider { get; } = new();
    public TestStorage Storage { get; } = new();
    public Guid CustomerId { get; private set; }
    public Guid OtherId { get; private set; }
    public Guid StaffId { get; private set; }
    public Guid AdminId { get; private set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:InitializeOnStartup"] = "false", ["BootstrapAdmin:Email"] = "", ["BootstrapAdmin:Password"] = "",
            ["JwtSettings:Secret"] = "test-only-secret-not-for-real-use-0123456789", ["Logging:LogLevel:Default"] = "Warning"
        }));
        builder.ConfigureServices(services =>
        {
            foreach (var item in services.Where(x => x.ImplementationType == typeof(GenerationWorker)).ToList()) services.Remove(item);
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            var connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("THEBOYS_TEST_SQL")
                ?? "Server=localhost;Trusted_Connection=True;TrustServerCertificate=True") { InitialCatalog = dbName };
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connection.ConnectionString));
            services.RemoveAll<IModelGenerationProvider>(); services.AddSingleton<IModelGenerationProvider>(Provider);
            services.RemoveAll<IModelStorage>(); services.AddSingleton<IModelStorage>(Storage);
        });
    }

    public async Task InitializeAsync()
    {
        _ = CreateClient();
        await DatabaseInitializer.InitializeAsync(Services, Services.GetRequiredService<IConfiguration>());
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        var users = new[]
        {
            new User { Email = "customer@example.test", FullName = "Customer", Role = UserRole.Customer },
            new User { Email = "other@example.test", FullName = "Other", Role = UserRole.Customer },
            new User { Email = "staff@example.test", FullName = "Staff", Role = UserRole.Staff },
            new User { Email = "admin@example.test", FullName = "Admin", Role = UserRole.Admin }
        };
        foreach (var user in users) user.PasswordHash = hasher.HashPassword(user, "Test-password-123!");
        db.Users.AddRange(users); await db.SaveChangesAsync();
        CustomerId = users[0].Id; OtherId = users[1].Id; StaffId = users[2].Id; AdminId = users[3].Id;
    }

    public async Task<HttpClient> Client(Guid id)
    {
        using var scope = Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.SingleAsync(x => x.Id == id);
        var token = System.Text.Json.JsonSerializer.SerializeToElement(Services.GetRequiredService<TokenService>().Create(user)).GetProperty("accessToken").GetString();
        var client = CreateClient(); client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public async Task Step(Guid id)
    {
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<GenerationProcessor>().ProcessAsync(id, CancellationToken.None);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        using (var scope = Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            if (db.Database.GetDbConnection().Database != dbName || !dbName.StartsWith("TheBoys_Test_")) throw new InvalidOperationException("Unsafe test database cleanup");
            await db.Database.EnsureDeletedAsync();
        }
        Storage.Cleanup();
        await base.DisposeAsync();
    }
}

public sealed class FakeProvider : IModelGenerationProvider
{
    public bool IsConfigured { get; set; } = true;
    public string Model => "meshy-6-lite";
    public int CreateCount { get; private set; }
    public bool FailGeneration { get; set; }
    public bool UncertainCreate { get; set; }
    public bool TransientGet { get; set; }
    public Task<string> CreateAsync(GenerationTask task, CancellationToken ct)
    {
        CreateCount++;
        if (UncertainCreate) throw new ProviderException("Uncertain submission", uncertain: true);
        return Task.FromResult($"{task.Id:N}-{task.RequestedHeightMm}-{task.Stage}");
    }
    public Task<ProviderTask> GetAsync(GenerationStage stage, string taskId, CancellationToken ct)
    {
        if (TransientGet) throw new ProviderException("Try later", transient: true);
        var height = int.Parse(taskId.Split('-')[1]);
        return Task.FromResult(new ProviderTask(FailGeneration && stage == GenerationStage.Generate ? "FAILED" : "SUCCEEDED", 100,
            new Dictionary<string, string> { ["glb"] = "https://assets.meshy.ai/model.glb", ["stl"] = "https://assets.meshy.ai/model.stl" },
            PrintingTests.Analysis(height), null, null));
    }
}

public sealed class TestStorage : IModelStorage
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "TheBoys_Test_" + Guid.NewGuid().ToString("N"));
    public string ModelKey(Guid id, string kind) => $"{id}.{kind}";
    public string ResolvePath(string key) => Path.Combine(root, key);
    public async Task<string> SaveImageAsync(Guid id, byte[] data, string contentType, CancellationToken ct)
    {
        Directory.CreateDirectory(root); var key = id + ".png";
        await File.WriteAllBytesAsync(ResolvePath(key), data, ct); return key;
    }
    public Task<string> ReadImageDataUriAsync(string key, CancellationToken ct) => Task.FromResult("data:image/png;base64,AA==");
    public async Task<string> DownloadAsync(Guid id, string kind, string url, CancellationToken ct)
    {
        Directory.CreateDirectory(root); var key = ModelKey(id, kind);
        await File.WriteAllBytesAsync(ResolvePath(key), kind == "stl" ? PrintingTests.Stl() : new byte[12], ct);
        return key;
    }
    public void Cleanup()
    {
        var parent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(root).StartsWith(parent, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(root).StartsWith("TheBoys_Test_")) throw new InvalidOperationException("Unsafe cleanup");
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
