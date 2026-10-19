using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TheBoys.Application.Printing;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;
using TheBoys.Infrastructure.Persistence;

namespace TheBoys.WebApi;

public static class DatabaseInitializer
{
    public static readonly Guid DemoPackageId = Guid.Parse("39000000-0000-0000-0000-000000000001");
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration configuration)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
        if (!await db.PricingPackages.AnyAsync(x => x.PackageCode == "TIER_STANDARD_390"))
        {
            var pricing = scope.ServiceProvider.GetRequiredService<IOptions<PrintPricingOptions>>().Value;
            db.PricingPackages.Add(new PricingPackage { Id = DemoPackageId, PackageCode = "TIER_STANDARD_390",
                Name = "Tượng demo 10–12 cm", PriceVnd = pricing.DemoUnitPriceVnd, PartnerBaseCostVnd = 120000,
                Material = "Resin", HeightRangeCm = "10–12 cm", MinHeightMm = 100, MaxHeightMm = 120 });
        }
        var email = configuration["BootstrapAdmin:Email"]?.Trim().ToLowerInvariant();
        var password = configuration["BootstrapAdmin:Password"];
        if (!string.IsNullOrWhiteSpace(email) && !await db.Users.AnyAsync(x => x.Role == UserRole.Admin))
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length < 12 || password.Length > 128)
                throw new InvalidOperationException("BootstrapAdmin:Password cần 12–128 ký tự.");
            if (await db.Users.IgnoreQueryFilters().AnyAsync(x => x.Email == email))
                throw new InvalidOperationException("Email bootstrap đã tồn tại; không tự nâng quyền tài khoản có sẵn.");
            var admin = new User { Email = email, FullName = "Administrator", Role = UserRole.Admin };
            admin.PasswordHash = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>().HashPassword(admin, password);
            db.Users.Add(admin);
        }
        await db.SaveChangesAsync();
    }
}
