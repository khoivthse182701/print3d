using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using TheBoys.Application;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Domain.Entities;
using TheBoys.Infrastructure;
using TheBoys.Infrastructure.Persistence;
using TheBoys.WebApi;
using TheBoys.WebApi.Security;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
});
builder.Services.AddProblemDetails();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddSingleton<TokenService>();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();

    var proxyConfig = builder.Configuration.GetSection("ReverseProxySettings");
    var knownProxies = proxyConfig.GetSection("KnownProxies").Get<string[]>() ?? [];
    var knownNetworks = proxyConfig.GetSection("KnownNetworks").Get<string[]>() ?? [];
    var hasTrustedProxy = false;

    foreach (var proxy in knownProxies)
    {
        if (System.Net.IPAddress.TryParse(proxy.Trim(), out var ip))
        {
            options.KnownProxies.Add(ip);
            hasTrustedProxy = true;
        }
    }

    foreach (var network in knownNetworks)
    {
        if (System.Net.IPNetwork.TryParse(network.Trim(), out var ipNetwork))
        {
            options.KnownIPNetworks.Add(ipNetwork);
            hasTrustedProxy = true;
        }
    }

    if (!hasTrustedProxy)
    {
        // Secure-by-default: if no trusted reverse proxies or networks are explicitly configured,
        // do not process any forwarded headers. Client-supplied headers are strictly ignored.
        options.ForwardLimit = 0;
        options.KnownProxies.Add(System.Net.IPAddress.None);
    }
    else
    {
        options.ForwardLimit = proxyConfig.GetValue<int?>("ForwardLimit") ?? 1;
    }
});

var jwt = builder.Configuration.GetSection("JwtSettings").Get<JwtOptions>() ?? new();
if (string.IsNullOrEmpty(jwt.Secret) && builder.Environment.IsDevelopment())
    jwt.Secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
if (Encoding.UTF8.GetByteCount(jwt.Secret) < 32 || jwt.AccessTokenMinutes is < 1 or > 1440)
    throw new InvalidOperationException("Cấu hình JwtSettings:Secret (ít nhất 32 bytes) và AccessTokenMinutes (1–1440).");
builder.Services.AddSingleton(jwt);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new()
    {
        ValidateIssuer = true, ValidIssuer = jwt.Issuer, ValidateAudience = true, ValidAudience = jwt.Audience,
        ValidateLifetime = true, ValidateIssuerSigningKey = true, RequireSignedTokens = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256], RoleClaimType = "role", NameClaimType = "sub", ClockSkew = TimeSpan.FromSeconds(15)
    };
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            if (!Guid.TryParse(context.Principal?.FindFirstValue("sub"), out var id)) { context.Fail("Invalid subject"); return; }
            var db = context.HttpContext.RequestServices.GetRequiredService<IApplicationDbContext>();
            var clock = context.HttpContext.RequestServices.GetRequiredService<IClock>();
            var now = clock.UtcNow;

            var sidClaim = context.Principal?.FindFirstValue("sid");
            if (Guid.TryParse(sidClaim, out var sessionId))
            {
                var session = await db.RefreshSessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sessionId, context.HttpContext.RequestAborted);
                if (session == null || !session.IsActive(now))
                {
                    context.Fail("Session revoked or expired");
                    return;
                }
            }

            var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, context.HttpContext.RequestAborted);
            if (user == null ||
                user.AccountStatus == TheBoys.Domain.Enums.AccountStatus.Locked ||
                (user.LockoutEndUtc.HasValue && user.LockoutEndUtc.Value > now) ||
                Roles.Name(user.Role) == "disabled")
            {
                context.Fail("Account unavailable");
                return;
            }

            var userRoleCache = context.HttpContext.RequestServices.GetRequiredService<IUserRoleCache>();
            var cachedRole = Roles.Name(user.Role);
            userRoleCache.Set(id, cachedRole, TimeSpan.FromSeconds(60));

            var identity = (ClaimsIdentity)context.Principal!.Identity!;
            foreach (var claim in identity.FindAll("role").ToList()) identity.RemoveClaim(claim);
            identity.AddClaim(new Claim("role", cachedRole));
        }
    };
});
builder.Services.AddAuthorization();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(builder.Configuration.GetSection("CorsSettings:AllowedOrigins").Get<string[]>() ?? ["http://localhost:5173"])
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context =>
    {
        var config = context.RequestServices.GetRequiredService<IConfiguration>().GetSection("RateLimitingSettings");
        var permit = config.GetValue<int?>("AuthPermitLimit") ?? 10;
        var window = config.GetValue<int?>("AuthWindowSeconds") ?? 60;
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            clientIp, _ => new() { PermitLimit = permit, Window = TimeSpan.FromSeconds(window), QueueLimit = 0 });
    });
    options.AddPolicy("generation", context =>
    {
        var config = context.RequestServices.GetRequiredService<IConfiguration>().GetSection("RateLimitingSettings");
        var permit = config.GetValue<int?>("AiGenerationPermitLimit") ?? 5;
        var window = config.GetValue<int?>("AiGenerationWindowSeconds") ?? 60;
        var key = context.User.FindFirstValue("sub") ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter(
            key, _ => new() { PermitLimit = permit, Window = TimeSpan.FromSeconds(window), QueueLimit = 0 });
    });
});

var app = builder.Build();
app.UseForwardedHeaders();

if (args.Contains("--initialize-database"))
{
    await DatabaseInitializer.InitializeAsync(app.Services, app.Configuration);
    return;
}
if (app.Configuration.GetValue<bool>("Database:InitializeOnStartup"))
    await DatabaseInitializer.InitializeAsync(app.Services, app.Configuration);

app.UseExceptionHandler(handler => handler.Run(async context =>
{
    var error = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    var isConflict = false;
    var current = error;
    while (current != null)
    {
        if (current is DbUpdateConcurrencyException) { isConflict = true; break; }
        if (current is SqlException sql && sql.Number is 2601 or 2627 or 1205) { isConflict = true; break; }
        current = current.InnerException;
    }

    var status = isConflict ? 409 : 500;
    context.Response.StatusCode = status;
    await Results.Problem(statusCode: status, title: status == 409
        ? "Dữ liệu vừa thay đổi hoặc yêu cầu đã tồn tại. Hãy tải lại và dùng cùng Idempotency-Key."
        : "Không xử lý được yêu cầu. Kiểm tra cấu hình database và log máy chủ.").ExecuteAsync(context);
}));
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}
if (!app.Environment.IsDevelopment()) app.UseHttpsRedirection();
app.UseCors();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapGet("/", () => Results.Redirect("/scalar/v1")).AllowAnonymous();

app.MapGet("/health/live", () => Results.Ok(new { status = "live", timestampUtc = DateTime.UtcNow })).AllowAnonymous();

static async Task<IResult> ExecuteHealthCheckAsync(ApplicationDbContext db, IConfiguration config, IWebHostEnvironment env)
{
    var now = DateTime.UtcNow;
    bool dbHealthy;
    string dbMessage = "Connected";
    try
    {
        dbHealthy = await db.Database.CanConnectAsync();
        if (!dbHealthy) dbMessage = "Cannot connect to database";
    }
    catch (Exception ex)
    {
        dbHealthy = false;
        dbMessage = ex.Message;
    }

    bool storageHealthy;
    string storageMessage = "Storage accessible";
    try
    {
        var storagePath = config["ModelStorage:BasePath"] ?? Path.Combine(env.ContentRootPath, "App_Data", "models");
        Directory.CreateDirectory(storagePath);
        var probeFile = Path.Combine(storagePath, $".health_{Guid.NewGuid():N}.tmp");
        await File.WriteAllTextAsync(probeFile, "health-check");
        File.Delete(probeFile);
        storageHealthy = true;
    }
    catch (Exception ex)
    {
        storageHealthy = false;
        storageMessage = ex.Message;
    }

    var meshyKey = config["AiProviders:Meshy:ApiKey"];
    var tripoKey = config["AiProviders:Tripo:ApiKey"];
    var aiChecks = new
    {
        meshy = new { configured = !string.IsNullOrWhiteSpace(meshyKey) },
        tripo = new { configured = !string.IsNullOrWhiteSpace(tripoKey) }
    };

    var isHealthy = dbHealthy && storageHealthy;
    var statusCode = isHealthy ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable;
    var response = new
    {
        status = isHealthy ? "ok" : "unhealthy",
        readiness = isHealthy ? "ready" : "unhealthy",
        timestampUtc = now,
        checks = new
        {
            database = new { healthy = dbHealthy, status = dbMessage },
            storage = new { healthy = storageHealthy, status = storageMessage },
            aiProviders = aiChecks
        }
    };

    return Results.Json(response, statusCode: statusCode);
}

app.MapGet("/health", (ApplicationDbContext db, IConfiguration config, IWebHostEnvironment env) => ExecuteHealthCheckAsync(db, config, env)).AllowAnonymous();
app.MapGet("/health/ready", (ApplicationDbContext db, IConfiguration config, IWebHostEnvironment env) => ExecuteHealthCheckAsync(db, config, env)).AllowAnonymous();

app.MapControllers();
app.Run();

public partial class Program { }
