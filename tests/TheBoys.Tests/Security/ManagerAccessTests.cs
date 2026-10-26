using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;
using TheBoys.Infrastructure.Persistence;
using TheBoys.WebApi;
using TheBoys.WebApi.Security;

namespace TheBoys.Tests.Security;

public class ManagerAccessTests(DemoFactory factory) : IClassFixture<DemoFactory>
{
    [Fact]
    public async Task Manager_CanReadOtherCustomersOperations_WithoutAdminPrivileges()
    {
        Guid managerId;
        var orderId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var manager = new User
            {
                Email = $"manager-{Guid.NewGuid():N}@example.test",
                FullName = "Operations manager",
                Role = UserRole.Manager,
                AccountStatus = AccountStatus.Active,
                EmailVerifiedAtUtc = DateTime.UtcNow
            };
            manager.NormalizedEmail = manager.Email.ToUpperInvariant();
            db.Users.Add(manager);
            managerId = manager.Id;
            db.Orders.Add(new Order
            {
                Id = orderId, OrderCode = $"MANAGER-TEST-{Guid.NewGuid():N}",
                UserId = factory.CustomerId, PricingPackageId = DatabaseInitializer.DemoPackageId,
                Status = OrderStatus.Paid, FulfillmentStatus = "AwaitingAssignment", OrderPaymentStatus = "Paid",
                UnitPriceSnapshot = 390000, SubTotalVnd = 390000, TotalAmount = 390000,
                ReceiverName = "Another customer's order", ReceiverPhone = "0900000000",
                StreetAddress = "Test address", District = "Test district", City = "Test city"
            });
            await db.SaveChangesAsync();
        }

        using var managerClient = await factory.Client(managerId);
        Assert.Equal("manager", Roles.Name(UserRole.Manager));
        foreach (var endpoint in new[] { "/api/orders", $"/api/v2/orders/{orderId}", $"/api/v2/orders/{orderId}/timeline", "/api/generations", "/api/v2/partner-labs", "/api/v2/defect-reports", "/api/v2/admin/observability" })
        {
            using var response = await managerClient.GetAsync(endpoint);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        using var adminResponse = await managerClient.GetAsync("/api/admin/users");
        Assert.Equal(HttpStatusCode.Forbidden, adminResponse.StatusCode);
        using var customerClient = await factory.Client(factory.OtherId);
        using var customerResponse = await customerClient.GetAsync($"/api/v2/orders/{orderId}");
        Assert.Equal(HttpStatusCode.Forbidden, customerResponse.StatusCode);
    }
}
