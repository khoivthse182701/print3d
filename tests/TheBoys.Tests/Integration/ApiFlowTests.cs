using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;
using TheBoys.Infrastructure.Persistence;
using TheBoys.WebApi;

namespace TheBoys.Tests;

public class ApiFlowTests(DemoFactory factory) : IClassFixture<DemoFactory>
{
    [Fact]
    public async Task UploadQuoteReviewOrderAndRolesUseRealSqlAndMockedProvider()
    {
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/generations")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await anonymous.PostAsJsonAsync("/api/auth/register",
            new { email = "spoof@example.test", fullName = "Spoof", password = "Example-password-123!", role = "admin" })).StatusCode);
        var login = await anonymous.PostAsJsonAsync("/api/auth/login", new { email = "customer@example.test", password = "Test-password-123!" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal("cus", (await Json(login)).GetProperty("user").GetProperty("role").GetString());

        using var customer = await factory.Client(factory.CustomerId);
        using var other = await factory.Client(factory.OtherId);
        using var staff = await factory.Client(factory.StaffId);
        using var admin = await factory.Client(factory.AdminId);
        var key = Guid.NewGuid().ToString();
        var created = await Upload(customer, key);
        Assert.Equal(HttpStatusCode.Accepted, created.StatusCode);
        var id = (await Json(created)).GetProperty("id").GetGuid();
        Assert.Equal(id, (await Json(await Upload(customer, key))).GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.Conflict, (await Upload(customer, key, 120)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Upload(customer, Guid.NewGuid().ToString(), 121)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/generations/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/generations/{id}/files/image")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await customer.GetAsync($"/api/generations/{id}/quote?packageId={DatabaseInitializer.DemoPackageId}")).StatusCode);
        var before = factory.Provider.CreateCount;
        for (var i = 0; i < 8; i++) await factory.Step(id);
        Assert.Equal(before + 4, factory.Provider.CreateCount);
        var completed = await Json(await customer.GetAsync($"/api/generations/{id}"));
        Assert.Equal("succeeded", completed.GetProperty("status").GetString());
        Assert.Equal(100m, completed.GetProperty("dimensions").GetProperty("heightMm").GetDecimal());
        Assert.Equal(120m, completed.GetProperty("modelVolumeCm3").GetDecimal());
        Assert.Equal(HttpStatusCode.OK, (await customer.GetAsync($"/api/generations/{id}/files/stl")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/generations/{id}/files/stl")).StatusCode);
        var quote = await Json(await customer.GetAsync($"/api/generations/{id}/quote?packageId={DatabaseInitializer.DemoPackageId}&quantity=2"));
        Assert.Equal(780000m, quote.GetProperty("totalVnd").GetDecimal());

        var orderKey = Guid.NewGuid().ToString();
        var orderBody = new { generationTaskId = id, pricingPackageId = DatabaseInitializer.DemoPackageId, quantity = 2,
            expectedTotalVnd = 780000, receiverName = "Demo Customer", receiverPhone = "0900000000",
            streetAddress = "1 Demo", district = "Demo", city = "HCM" };
        customer.DefaultRequestHeaders.Add("Idempotency-Key", orderKey);
        var orderResponse = await customer.PostAsJsonAsync("/api/orders", orderBody);
        Assert.Equal(HttpStatusCode.Created, orderResponse.StatusCode);
        var orderId = (await Json(orderResponse)).GetProperty("id").GetGuid();
        Assert.Equal(orderId, (await Json(await customer.PostAsJsonAsync("/api/orders", orderBody))).GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/orders/{orderId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.PatchAsJsonAsync($"/api/orders/{orderId}/status", new { status = "printing", note = "Start print" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await staff.PatchAsJsonAsync($"/api/orders/{orderId}/status", new { status = "printing", note = "Start print" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.PostAsJsonAsync($"/api/generations/{id}/review", new { approved = true, note = "Self approval" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await staff.PostAsJsonAsync($"/api/generations/{id}/review", new { approved = true, note = "Đã kiểm tra file và support trong slicer" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await staff.PatchAsJsonAsync($"/api/orders/{orderId}/status", new { status = "printing", note = "In demo offline" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await staff.PatchAsJsonAsync($"/api/orders/{orderId}/status", new { status = "delivered", note = "Skip steps" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PatchAsJsonAsync($"/api/admin/users/{factory.OtherId}/role", new { role = "admin" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PatchAsJsonAsync($"/api/admin/pricing-packages/{DatabaseInitializer.DemoPackageId}", new { priceVnd = 400000, isActive = true })).StatusCode);
        Assert.Equal(780000m, (await Json(await customer.GetAsync($"/api/orders/{orderId}"))).GetProperty("totalVnd").GetDecimal());
        customer.DefaultRequestHeaders.Remove("Idempotency-Key"); customer.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.Conflict, (await customer.PostAsJsonAsync("/api/orders", orderBody)).StatusCode);
        await admin.PatchAsJsonAsync($"/api/admin/pricing-packages/{DatabaseInitializer.DemoPackageId}", new { priceVnd = 390000, isActive = true });

        // Existing JWT must lose staff permissions immediately after demotion.
        Assert.Equal(HttpStatusCode.OK, (await admin.PatchAsJsonAsync($"/api/admin/users/{factory.StaffId}/role", new { role = "cus" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsJsonAsync($"/api/generations/{id}/review", new { approved = true, note = "Old staff token" })).StatusCode);
        await admin.PatchAsJsonAsync($"/api/admin/users/{factory.StaffId}/role", new { role = "staff" });
    }

    [Fact]
    public async Task WorkerRefundsOnceAndNeverResubmitsUncertainPosts()
    {
        var userId = await NewCustomer();
        using var client = await factory.Client(userId);
        var id = (await Json(await Upload(client, Guid.NewGuid().ToString()))).GetProperty("id").GetGuid();
        try
        {
            await factory.Step(id); // Persist provider ID.
            factory.Provider.TransientGet = true;
            await factory.Step(id);
            Assert.Equal("processing", (await Json(await client.GetAsync($"/api/generations/{id}"))).GetProperty("status").GetString());
            factory.Provider.TransientGet = false; factory.Provider.FailGeneration = true;
            await factory.Step(id); await factory.Step(id);
            var me = await Json(await client.GetAsync("/api/auth/me"));
            Assert.Equal(2, me.GetProperty("freeTrialRemaining").GetInt32());
            Assert.Equal(0, me.GetProperty("totalUsed").GetInt32());
            factory.Provider.FailGeneration = false;

            var second = (await Json(await Upload(client, Guid.NewGuid().ToString()))).GetProperty("id").GetGuid();
            factory.Provider.UncertainCreate = true;
            var count = factory.Provider.CreateCount;
            await factory.Step(second); await factory.Step(second);
            Assert.Equal(count + 1, factory.Provider.CreateCount);
            Assert.Equal("needsAttention", (await Json(await client.GetAsync($"/api/generations/{second}"))).GetProperty("status").GetString());
            Assert.Equal(1, (await Json(await client.GetAsync("/api/auth/me"))).GetProperty("freeTrialRemaining").GetInt32());
            factory.Provider.UncertainCreate = false;
            using var staff = await factory.Client(factory.StaffId);
            Assert.Equal(HttpStatusCode.OK, (await staff.PostAsJsonAsync($"/api/generations/{second}/recover", new { providerTaskId = $"{second:N}-100-Generate" })).StatusCode);
            for (var i = 0; i < 7; i++) await factory.Step(second);
            Assert.Equal("succeeded", (await Json(await client.GetAsync($"/api/generations/{second}"))).GetProperty("status").GetString());
        }
        finally { factory.Provider.TransientGet = false; factory.Provider.FailGeneration = false; factory.Provider.UncertainCreate = false; }
    }

    [Fact]
    public async Task MissingProviderKeyDoesNotChargeQuota()
    {
        var id = await NewCustomer(); using var client = await factory.Client(id);
        try
        {
            factory.Provider.IsConfigured = false;
            Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Upload(client, Guid.NewGuid().ToString())).StatusCode);
            Assert.Equal(2, (await Json(await client.GetAsync("/api/auth/me"))).GetProperty("freeTrialRemaining").GetInt32());
        }
        finally { factory.Provider.IsConfigured = true; }
    }

    private async Task<Guid> NewCustomer()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = new User { Email = $"{Guid.NewGuid():N}@example.test", FullName = "Test" };
        db.Users.Add(user); await db.SaveChangesAsync(); return user.Id;
    }

    [Fact]
    public async Task ConcurrentUploadsCannotSpendTheLastCreditTwice()
    {
        var userId = await NewCustomer();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.SingleAsync(x => x.Id == userId);
            user.FreeTrialRemaining = 1; await db.SaveChangesAsync();
        }
        using var first = await factory.Client(userId); using var second = await factory.Client(userId);
        var results = await Task.WhenAll(Upload(first, Guid.NewGuid().ToString()), Upload(second, Guid.NewGuid().ToString()));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.Accepted);
        Assert.Single(results, x => x.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.PaymentRequired);
        using var finalScope = factory.Services.CreateScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await finalDb.GenerationTasks.CountAsync(x => x.UserId == userId));
        Assert.Equal(0, (await finalDb.Users.SingleAsync(x => x.Id == userId)).FreeTrialRemaining);
    }

    private static async Task<HttpResponseMessage> Upload(HttpClient client, string key, int height = 100)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a6XcAAAAASUVORK5CYII=")), "image", "sample.png");
        form.Add(new StringContent(height.ToString()), "heightMm");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/generations/image") { Content = form };
        request.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(request);
    }
    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"HTTP {(int)response.StatusCode}: {body}");
        return JsonSerializer.Deserialize<JsonElement>(body);
    }
}
