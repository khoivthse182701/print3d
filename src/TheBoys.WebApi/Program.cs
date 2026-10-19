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
using TheBoys.Application;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Domain.Entities;
using TheBoys.Infrastructure;
using TheBoys.Infrastructure.Persistence;
using TheBoys.WebApi;
using TheBoys.WebApi.Security;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddSingleton<TokenService>();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
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
            var userRoleCache = context.HttpContext.RequestServices.GetRequiredService<IUserRoleCache>();
            var cachedRole = userRoleCache.Get(id);
            if (cachedRole == null)
            {
                var db = context.HttpContext.RequestServices.GetRequiredService<IApplicationDbContext>();
                var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, context.HttpContext.RequestAborted);
                if (user == null || Roles.Name(user.Role) == "disabled") { context.Fail("Account unavailable"); return; }
                cachedRole = Roles.Name(user.Role);
                userRoleCache.Set(id, cachedRole, TimeSpan.FromSeconds(60));
            }
            else if (cachedRole == "disabled")
            {
                context.Fail("Account unavailable");
                return;
            }
            var identity = (ClaimsIdentity)context.Principal!.Identity!;
            foreach (var claim in identity.FindAll("role").ToList()) identity.RemoveClaim(claim);
            identity.AddClaim(new Claim("role", cachedRole));
        }
    };
});
builder.Services.AddAuthorization();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(builder.Configuration.GetSection("CorsSettings:AllowedOrigins").Get<string[]>() ?? ["http://localhost:5173"])
    .AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context =>
    {
        var clientIp = context.Request.Headers["CF-Connecting-IP"].FirstOrDefault()
            ?? context.Request.Headers["X-Real-IP"].FirstOrDefault()
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            clientIp, _ => new() { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 });
    });
    options.AddPolicy("generation", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirstValue("sub") ?? "anonymous", _ => new() { PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
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
    var status = error switch
    {
        DbUpdateConcurrencyException => 409,
        DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } } => 409,
        _ => 500
    };
    context.Response.StatusCode = status;
    await Results.Problem(statusCode: status, title: status == 409
        ? "Dữ liệu vừa thay đổi hoặc yêu cầu đã tồn tại. Hãy tải lại và dùng cùng Idempotency-Key."
        : "Không xử lý được yêu cầu. Kiểm tra cấu hình database và log máy chủ.").ExecuteAsync(context);
}));
if (app.Environment.IsDevelopment()) app.MapOpenApi();
if (!app.Environment.IsDevelopment()) app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapControllers();
app.Run();

public partial class Program { }
