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
            db.PricingPackages.Add(new PricingPackage
            {
                Id = DemoPackageId,
                PackageCode = "TIER_STANDARD_390",
                Name = "Tượng demo 10–12 cm",
                PriceVnd = pricing.DemoUnitPriceVnd,
                PartnerBaseCostVnd = 120000,
                Material = "Resin",
                HeightRangeCm = "10–12 cm",
                MinHeightMm = 100,
                MaxHeightMm = 120
            });
        }
        var email = configuration["BootstrapAdmin:Email"]?.Trim().ToLowerInvariant();
        var password = configuration["BootstrapAdmin:Password"];
        if (!string.IsNullOrWhiteSpace(email) && !await db.Users.AnyAsync(x => x.Role == UserRole.Admin))
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length < 12 || password.Length > 128)
                throw new InvalidOperationException("BootstrapAdmin:Password cần 12–128 ký tự.");
            if (await db.Users.IgnoreQueryFilters().AnyAsync(x => x.Email == email))
                throw new InvalidOperationException("Email bootstrap đã tồn tại; không tự nâng quyền tài khoản có sẵn.");

            var admin = new User
            {
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                FullName = "Administrator",
                Role = UserRole.Admin,
                AccountStatus = AccountStatus.Active,
                EmailVerifiedAtUtc = DateTime.UtcNow
            };
            admin.PasswordHash = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>().HashPassword(admin, password);
            admin.CreditAccount = CreditAccount.Create(admin.Id, 2, 0);
            db.Users.Add(admin);
        }

        // Seed development standard users for automated testing and local verification
        if (configuration.GetValue<bool>("Database:InitializeOnStartup") || configuration.GetValue<bool>("SeedDevUsers"))
        {
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
            var devUsers = new[]
            {
                new User { Email = "admin@123", NormalizedEmail = "ADMIN@123", FullName = "Quản Trị Viên", Role = UserRole.Admin, AccountStatus = AccountStatus.Active, EmailVerifiedAtUtc = DateTime.UtcNow },
                new User { Email = "customer@123", NormalizedEmail = "CUSTOMER@123", FullName = "Khách Hàng Mẫu", Role = UserRole.Customer, AccountStatus = AccountStatus.Active, EmailVerifiedAtUtc = DateTime.UtcNow },
                new User { Email = "staff@123", NormalizedEmail = "STAFF@123", FullName = "Nhân Viên Vận Hành", Role = UserRole.Staff, AccountStatus = AccountStatus.Active, EmailVerifiedAtUtc = DateTime.UtcNow },
                new User { Email = "manager@123", NormalizedEmail = "MANAGER@123", FullName = "Quản Lý Vận Hành", Role = UserRole.Manager, AccountStatus = AccountStatus.Active, EmailVerifiedAtUtc = DateTime.UtcNow }
            };

            foreach (var devUser in devUsers)
            {
                if (!await db.Users.IgnoreQueryFilters().AnyAsync(x => x.Email == devUser.Email))
                {
                    devUser.PasswordHash = hasher.HashPassword(devUser, "Admin@123456");
                    devUser.CreditAccount = CreditAccount.Create(devUser.Id, 10, 0);
                    db.Users.Add(devUser);
                }
            }
        }

        await db.SaveChangesAsync();
    }
}
