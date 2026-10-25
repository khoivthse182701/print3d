using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;
using TheBoys.Infrastructure.Persistence;
using TheBoys.WebApi;
using TheBoys.WebApi.Security;

namespace TheBoys.Tests.Integration;

public class ManagerRoleFlowTests(DemoFactory factory) : IClassFixture<DemoFactory>
{
    [Fact]
    public async Task AC01_And_AC02_ManagerAuthenticationAndAdminRoleManagement()
    {
        using var anonymous = factory.CreateClient();

        // AC-01: Manager login returns 200, JWT token, user.role == "manager"
        var login = await anonymous.PostAsJsonAsync("/api/auth/login", new { email = "manager@example.test", password = "Test-password-123!" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var loginJson = await Json(login);
        Assert.Equal("manager", loginJson.GetProperty("user").GetProperty("role").GetString());
        var tokenString = loginJson.GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrEmpty(tokenString));

        // AC-01: Token claim inspection
        var jwtHandler = new JwtSecurityTokenHandler();
        var jwt = jwtHandler.ReadJwtToken(tokenString);
        var roleClaim = jwt.Claims.FirstOrDefault(c => c.Type == "role");
        Assert.NotNull(roleClaim);
        Assert.Equal("manager", roleClaim.Value);

        // AC-01: /api/auth/me returns role "manager"
        using var managerClient = await factory.Client(factory.ManagerId);
        var me = await Json(await managerClient.GetAsync("/api/auth/me"));
        Assert.Equal("manager", me.GetProperty("role").GetString());

        // AC-02: Public register payload with role: "manager" is rejected (400) and creates no privileged account
        var regResponse = await anonymous.PostAsJsonAsync("/api/auth/register",
            new { email = "spoof-mgr@example.test", fullName = "Spoof Mgr", password = "Example-password-123!", role = "manager" });
        Assert.Equal(HttpStatusCode.BadRequest, regResponse.StatusCode);

        // AC-02: Only Admin can grant / revoke Manager role
        using var adminClient = await factory.Client(factory.AdminId);
        using var customerClient = await factory.Client(factory.CustomerId);
        using var staffClient = await factory.Client(factory.StaffId);

        var tempTargetId = await NewCustomer();

        // Manager / Staff / Customer get 403 attempting role update
        Assert.Equal(HttpStatusCode.Forbidden,
            (await managerClient.PatchAsJsonAsync($"/api/admin/users/{tempTargetId}/role", new { role = "manager" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await staffClient.PatchAsJsonAsync($"/api/admin/users/{tempTargetId}/role", new { role = "manager" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await customerClient.PatchAsJsonAsync($"/api/admin/users/{tempTargetId}/role", new { role = "manager" })).StatusCode);

        // Admin grants role: "manager"
        var grantRes = await adminClient.PatchAsJsonAsync($"/api/admin/users/{tempTargetId}/role", new { role = "manager" });
        Assert.Equal(HttpStatusCode.OK, grantRes.StatusCode);
        var grantJson = await Json(grantRes);
        Assert.Equal("manager", grantJson.GetProperty("role").GetString());

        // Admin grants with alias "mgr"
        var grantAliasRes = await adminClient.PatchAsJsonAsync($"/api/admin/users/{tempTargetId}/role", new { role = "mgr" });
        Assert.Equal(HttpStatusCode.OK, grantAliasRes.StatusCode);
        Assert.Equal("manager", (await Json(grantAliasRes)).GetProperty("role").GetString());

        // Admin revokes back to "cus"
        var revokeRes = await adminClient.PatchAsJsonAsync($"/api/admin/users/{tempTargetId}/role", new { role = "cus" });
        Assert.Equal(HttpStatusCode.OK, revokeRes.StatusCode);
        Assert.Equal("cus", (await Json(revokeRes)).GetProperty("role").GetString());

        // Invalid role returns 400
        var invalidRoleRes = await adminClient.PatchAsJsonAsync($"/api/admin/users/{tempTargetId}/role", new { role = "superuser" });
        Assert.Equal(HttpStatusCode.BadRequest, invalidRoleRes.StatusCode);

        // Self-role change guard: Admin cannot change own role via HTTP (409 Conflict: CannotChangeOwnRole)
        var selfRoleRes = await adminClient.PatchAsJsonAsync($"/api/admin/users/{factory.AdminId}/role", new { role = "manager" });
        Assert.Equal(HttpStatusCode.Conflict, selfRoleRes.StatusCode);

        // Two-admin scenario: creating a second admin allows demoting that second admin
        var tempAdminId = await NewCustomer();
        await adminClient.PatchAsJsonAsync($"/api/admin/users/{tempAdminId}/role", new { role = "admin" });
        Assert.Equal(HttpStatusCode.OK,
            (await adminClient.PatchAsJsonAsync($"/api/admin/users/{tempAdminId}/role", new { role = "cus" })).StatusCode);

        // LastAdmin domain guard: when caller ID differs from target ID (bypassing CannotChangeOwnRole)
        // and target is the sole remaining admin, UpdateUserRoleCommandHandler enforces the LastAdmin invariant
        using (var scope = factory.Services.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<MediatR.ISender>();
            var lastAdminResult = await mediator.Send(new TheBoys.Application.Admin.Commands.UpdateUserRoleCommand(
                CurrentAdminUserId: Guid.NewGuid(),
                TargetUserId: factory.AdminId,
                Role: "cus"));
            Assert.True(lastAdminResult.IsFailure);
            Assert.Equal("LastAdmin", lastAdminResult.Error.Code);
            Assert.Equal("Phải giữ ít nhất một Admin.", lastAdminResult.Error.Message);
        }
    }

    [Fact]
    public async Task AC03_ManagerOperationalReadsAcrossOwners()
    {
        using var anonymous = factory.CreateClient();
        var customerId = await NewCustomer(10);
        var otherId = await NewCustomer(10);
        using var customer = await factory.Client(customerId);
        using var other = await factory.Client(otherId);
        using var manager = await factory.Client(factory.ManagerId);

        // Create generation task and step to Succeeded
        var taskId = (await Json(await Upload(customer, Guid.NewGuid().ToString()))).GetProperty("id").GetGuid();
        for (var i = 0; i < 8; i++) await factory.Step(taskId);

        // Create order
        var orderKey = Guid.NewGuid().ToString();
        var orderBody = new
        {
            generationTaskId = taskId,
            pricingPackageId = DatabaseInitializer.DemoPackageId,
            quantity = 1,
            expectedTotalVnd = 390000,
            receiverName = "Customer Test",
            receiverPhone = "0911222333",
            streetAddress = "123 Main St",
            district = "District 1",
            city = "HCM"
        };
        customer.DefaultRequestHeaders.Remove("Idempotency-Key");
        customer.DefaultRequestHeaders.Add("Idempotency-Key", orderKey);
        var orderResponse = await customer.PostAsJsonAsync("/api/orders", orderBody);
        var orderId = (await Json(orderResponse)).GetProperty("id").GetGuid();

        // 1. Unauthenticated gets 401
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/generations")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/generations/{taskId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/generations/{taskId}/files/image")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/generations/{taskId}/files/stl")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/generations/{taskId}/quote?packageId={DatabaseInitializer.DemoPackageId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/orders")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/orders/{orderId}")).StatusCode);

        // 2. Other Customer gets 404 for another user's resources
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/generations/{taskId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/generations/{taskId}/files/image")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/generations/{taskId}/files/stl")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/generations/{taskId}/quote?packageId={DatabaseInitializer.DemoPackageId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/orders/{orderId}")).StatusCode);

        // 3. Manager can read all operational data across owners
        var genList = await Json(await manager.GetAsync("/api/generations"));
        Assert.Contains(genList.EnumerateArray(), item => item.GetProperty("id").GetGuid() == taskId);

        var genDetailRes = await manager.GetAsync($"/api/generations/{taskId}");
        Assert.Equal(HttpStatusCode.OK, genDetailRes.StatusCode);
        var genDetail = await Json(genDetailRes);
        Assert.True(genDetail.TryGetProperty("providerTaskIds", out var pids) && pids.ValueKind != JsonValueKind.Null);

        Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync($"/api/generations/{taskId}/files/image")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync($"/api/generations/{taskId}/files/stl")).StatusCode);

        var quoteRes = await manager.GetAsync($"/api/generations/{taskId}/quote?packageId={DatabaseInitializer.DemoPackageId}");
        Assert.Equal(HttpStatusCode.OK, quoteRes.StatusCode);
        Assert.Equal(390000m, (await Json(quoteRes)).GetProperty("totalVnd").GetDecimal());

        var orderList = await Json(await manager.GetAsync("/api/orders"));
        Assert.Contains(orderList.EnumerateArray(), item => item.GetProperty("id").GetGuid() == orderId);

        var orderDetailRes = await manager.GetAsync($"/api/orders/{orderId}");
        Assert.Equal(HttpStatusCode.OK, orderDetailRes.StatusCode);
        Assert.Equal(orderId, (await Json(orderDetailRes)).GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task AC04_ManagerReviewsRecoversAndExecutesOrderTransitionsWithGuards()
    {
        var customerId = await NewCustomer(10);
        using var customer = await factory.Client(customerId);
        using var manager = await factory.Client(factory.ManagerId);

        // === 1. Review & Guards ===
        var taskId = (await Json(await Upload(customer, Guid.NewGuid().ToString()))).GetProperty("id").GetGuid();

        // Review guard: task not yet Succeeded -> 409 Conflict
        var prematureReview = await manager.PostAsJsonAsync($"/api/generations/{taskId}/review",
            new { approved = true, note = "Premature review" });
        Assert.Equal(HttpStatusCode.Conflict, prematureReview.StatusCode);

        // Step to succeeded
        for (var i = 0; i < 8; i++) await factory.Step(taskId);

        // Review guard: empty note -> 400 Bad Request
        var emptyNoteReview = await manager.PostAsJsonAsync($"/api/generations/{taskId}/review",
            new { approved = true, note = "   " });
        Assert.Equal(HttpStatusCode.BadRequest, emptyNoteReview.StatusCode);

        // Manager reviews Reject
        var rejectRes = await manager.PostAsJsonAsync($"/api/generations/{taskId}/review",
            new { approved = false, note = "Need thicker base" });
        Assert.Equal(HttpStatusCode.OK, rejectRes.StatusCode);
        var rejectedJson = await Json(rejectRes);
        Assert.False(rejectedJson.GetProperty("isApprovedForPrinting").GetBoolean());
        Assert.Equal("rejected", rejectedJson.GetProperty("reviewStatus").GetString());

        // Manager reviews Approve
        var approveRes = await manager.PostAsJsonAsync($"/api/generations/{taskId}/review",
            new { approved = true, note = "Approved after base thickness verified" });
        Assert.Equal(HttpStatusCode.OK, approveRes.StatusCode);
        var approvedJson = await Json(approveRes);
        Assert.True(approvedJson.GetProperty("isApprovedForPrinting").GetBoolean());
        Assert.Equal("approved", approvedJson.GetProperty("reviewStatus").GetString());

        // === 2. Recover & Guards ===
        var recoverTaskId = (await Json(await Upload(customer, Guid.NewGuid().ToString()))).GetProperty("id").GetGuid();
        try
        {
            factory.Provider.UncertainCreate = true;
            await factory.Step(recoverTaskId);
            await factory.Step(recoverTaskId);
            Assert.Equal("needsAttention", (await Json(await customer.GetAsync($"/api/generations/{recoverTaskId}"))).GetProperty("status").GetString());
            factory.Provider.UncertainCreate = false;

            // Manager recovers NeedsAttention task -> 200 OK
            var recoverRes = await manager.PostAsJsonAsync($"/api/generations/{recoverTaskId}/recover",
                new { providerTaskId = $"{recoverTaskId:N}-100-Generate" });
            Assert.Equal(HttpStatusCode.OK, recoverRes.StatusCode);
            Assert.Equal("processing", (await Json(recoverRes)).GetProperty("status").GetString());

            // Recover guard: task not in NeedsAttention -> 409 Conflict
            var invalidRecover = await manager.PostAsJsonAsync($"/api/generations/{recoverTaskId}/recover",
                new { providerTaskId = $"{recoverTaskId:N}-100-Generate" });
            Assert.Equal(HttpStatusCode.Conflict, invalidRecover.StatusCode);
        }
        finally
        {
            factory.Provider.UncertainCreate = false;
        }

        // === 3. Order Transitions, Draft Cancel & Domain Guards ===
        // Create an unapproved generation task and order to test TaskNotApproved guard
        var unapprovedTaskId = (await Json(await Upload(customer, Guid.NewGuid().ToString()))).GetProperty("id").GetGuid();
        for (var i = 0; i < 8; i++) await factory.Step(unapprovedTaskId);

        var order1Body = new
        {
            generationTaskId = unapprovedTaskId,
            pricingPackageId = DatabaseInitializer.DemoPackageId,
            quantity = 1,
            expectedTotalVnd = 390000,
            receiverName = "Customer Test",
            receiverPhone = "0911222333",
            streetAddress = "123 Main St",
            district = "District 1",
            city = "HCM"
        };
        customer.DefaultRequestHeaders.Remove("Idempotency-Key");
        customer.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var order1 = (await Json(await customer.PostAsJsonAsync("/api/orders", order1Body))).GetProperty("id").GetGuid();

        // Guard: Transition Draft -> Printing fails if model is unapproved (409 Conflict)
        var unapprovedTransition = await manager.PatchAsJsonAsync($"/api/orders/{order1}/status",
            new { status = "printing", note = "Start printing without approval" });
        Assert.Equal(HttpStatusCode.Conflict, unapprovedTransition.StatusCode);

        // Cancel Draft Order by Manager
        var cancelDraftRes = await manager.PostAsJsonAsync($"/api/orders/{order1}/cancel", new { });
        Assert.Equal(HttpStatusCode.OK, cancelDraftRes.StatusCode);
        Assert.Equal("cancelled", (await Json(cancelDraftRes)).GetProperty("status").GetString());

        // Create order on the APPROVED task (taskId) to test full lifecycle transitions
        var order2Body = new
        {
            generationTaskId = taskId,
            pricingPackageId = DatabaseInitializer.DemoPackageId,
            quantity = 1,
            expectedTotalVnd = 390000,
            receiverName = "Customer Test",
            receiverPhone = "0911222333",
            streetAddress = "123 Main St",
            district = "District 1",
            city = "HCM"
        };
        customer.DefaultRequestHeaders.Remove("Idempotency-Key");
        customer.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var order2 = (await Json(await customer.PostAsJsonAsync("/api/orders", order2Body))).GetProperty("id").GetGuid();

        // Draft -> Printing (200 OK)
        var toPrinting = await manager.PatchAsJsonAsync($"/api/orders/{order2}/status",
            new { status = "printing", note = "Manager verified and sent to queue" });
        Assert.Equal(HttpStatusCode.OK, toPrinting.StatusCode);
        Assert.Equal("printing", (await Json(toPrinting)).GetProperty("status").GetString());

        // Cancel guard: Cannot cancel order that has started printing (409 Conflict)
        var cancelPrintingRes = await manager.PostAsJsonAsync($"/api/orders/{order2}/cancel", new { });
        Assert.Equal(HttpStatusCode.Conflict, cancelPrintingRes.StatusCode);

        // Guard: Invalid transition Printing -> Delivered directly (409 Conflict)
        var invalidSkip = await manager.PatchAsJsonAsync($"/api/orders/{order2}/status",
            new { status = "delivered", note = "Skip packaging and shipping" });
        Assert.Equal(HttpStatusCode.Conflict, invalidSkip.StatusCode);

        // Printing -> Packaged (200 OK)
        var toPackaged = await manager.PatchAsJsonAsync($"/api/orders/{order2}/status",
            new { status = "packaged", note = "Cleaned and boxed" });
        Assert.Equal(HttpStatusCode.OK, toPackaged.StatusCode);
        Assert.Equal("packaged", (await Json(toPackaged)).GetProperty("status").GetString());

        // Packaged -> Shipping (200 OK)
        var toShipping = await manager.PatchAsJsonAsync($"/api/orders/{order2}/status",
            new { status = "shipping", note = "Handed to courier", trackingNumber = "VNPOST-123456" });
        Assert.Equal(HttpStatusCode.OK, toShipping.StatusCode);
        Assert.Equal("shipping", (await Json(toShipping)).GetProperty("status").GetString());

        // Shipping -> Delivered (200 OK)
        var toDelivered = await manager.PatchAsJsonAsync($"/api/orders/{order2}/status",
            new { status = "delivered", note = "Customer confirmed receipt" });
        Assert.Equal(HttpStatusCode.OK, toDelivered.StatusCode);
        Assert.Equal("delivered", (await Json(toDelivered)).GetProperty("status").GetString());

        // Guard: Backwards transition Delivered -> Printing (409 Conflict)
        var backwards = await manager.PatchAsJsonAsync($"/api/orders/{order2}/status",
            new { status = "printing", note = "Rollback" });
        Assert.Equal(HttpStatusCode.Conflict, backwards.StatusCode);
    }

    [Fact]
    public async Task AC05_ManagerCannotAccessAdminEndpointsAndNoSideEffectsOccur()
    {
        using var manager = await factory.Client(factory.ManagerId);

        // Record initial state in DB
        decimal initialPackagePrice;
        int initialCustomerCredits;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            initialPackagePrice = (await db.PricingPackages.SingleAsync(x => x.Id == DatabaseInitializer.DemoPackageId)).PriceVnd;
            initialCustomerCredits = (await db.Users.SingleAsync(x => x.Id == factory.CustomerId)).PurchasedRemaining;
        }

        // 1. GET /api/admin/users -> 403
        var listUsersRes = await manager.GetAsync("/api/admin/users");
        Assert.Equal(HttpStatusCode.Forbidden, listUsersRes.StatusCode);

        // 2. PATCH /api/admin/users/{id}/role -> 403
        var changeRoleRes = await manager.PatchAsJsonAsync($"/api/admin/users/{factory.CustomerId}/role", new { role = "admin" });
        Assert.Equal(HttpStatusCode.Forbidden, changeRoleRes.StatusCode);

        // 3. POST /api/admin/users/{id}/credits -> 403
        var addCreditsRes = await manager.PostAsJsonAsync($"/api/admin/users/{factory.CustomerId}/credits", new { amount = 50 });
        Assert.Equal(HttpStatusCode.Forbidden, addCreditsRes.StatusCode);

        // 4. PATCH /api/admin/pricing-packages/{id} -> 403
        var updatePriceRes = await manager.PatchAsJsonAsync($"/api/admin/pricing-packages/{DatabaseInitializer.DemoPackageId}",
            new { priceVnd = 999999, isActive = true });
        Assert.Equal(HttpStatusCode.Forbidden, updatePriceRes.StatusCode);

        // Verify NO side-effects in DB
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var customerUser = await db.Users.SingleAsync(x => x.Id == factory.CustomerId);
            Assert.Equal(UserRole.Customer, customerUser.Role);
            Assert.Equal(initialCustomerCredits, customerUser.PurchasedRemaining);

            var pkg = await db.PricingPackages.SingleAsync(x => x.Id == DatabaseInitializer.DemoPackageId);
            Assert.Equal(initialPackagePrice, pkg.PriceVnd);
        }
    }

    [Fact]
    public async Task AC06_StaleTokenReflectsRoleChangeOnNextRequestInSameInstance()
    {
        using var admin = await factory.Client(factory.AdminId);
        var targetCustomerId = await NewCustomer(10);
        using var targetCustomer = await factory.Client(targetCustomerId);

        // Setup a generation task owned by CustomerId
        var taskId = (await Json(await Upload(targetCustomer, Guid.NewGuid().ToString()))).GetProperty("id").GetGuid();
        for (var i = 0; i < 8; i++) await factory.Step(taskId);

        // Create a new user that starts as Customer
        var testUserId = await NewCustomer();
        using var testClient = await factory.Client(testUserId);

        // 1. As Customer, testClient cannot review another user's task (403 Forbidden)
        var cusReviewRes = await testClient.PostAsJsonAsync($"/api/generations/{taskId}/review",
            new { approved = true, note = "Customer attempt" });
        Assert.Equal(HttpStatusCode.Forbidden, cusReviewRes.StatusCode);

        // Also cannot read other's task (404 Not Found)
        Assert.Equal(HttpStatusCode.NotFound, (await testClient.GetAsync($"/api/generations/{taskId}")).StatusCode);

        // 2. Admin promotes testUser to Manager
        var promoteRes = await admin.PatchAsJsonAsync($"/api/admin/users/{testUserId}/role", new { role = "manager" });
        Assert.Equal(HttpStatusCode.OK, promoteRes.StatusCode);

        // WITHOUT logging in again, testClient's existing token immediately has Manager privileges!
        // Read cross-owner generation task: 200 OK
        var promotedReadRes = await testClient.GetAsync($"/api/generations/{taskId}");
        Assert.Equal(HttpStatusCode.OK, promotedReadRes.StatusCode);

        // Review generation task: 200 OK
        var promotedReviewRes = await testClient.PostAsJsonAsync($"/api/generations/{taskId}/review",
            new { approved = true, note = "Manager approved after promotion" });
        Assert.Equal(HttpStatusCode.OK, promotedReviewRes.StatusCode);

        // /api/auth/me reflects "manager"
        var mePromoted = await Json(await testClient.GetAsync("/api/auth/me"));
        Assert.Equal("manager", mePromoted.GetProperty("role").GetString());

        // 3. Admin demotes testUser back to Customer
        var demoteRes = await admin.PatchAsJsonAsync($"/api/admin/users/{testUserId}/role", new { role = "cus" });
        Assert.Equal(HttpStatusCode.OK, demoteRes.StatusCode);

        // WITHOUT logging in again, testClient's token IMMEDIATELY loses Manager privileges!
        var demotedReviewRes = await testClient.PostAsJsonAsync($"/api/generations/{taskId}/review",
            new { approved = true, note = "Demoted token attempt" });
        Assert.Equal(HttpStatusCode.Forbidden, demotedReviewRes.StatusCode);

        // Cross-owner read is immediately blocked (404 Not Found)
        var demotedReadRes = await testClient.GetAsync($"/api/generations/{taskId}");
        Assert.Equal(HttpStatusCode.NotFound, demotedReadRes.StatusCode);

        // /api/auth/me reflects "cus"
        var meDemoted = await Json(await testClient.GetAsync("/api/auth/me"));
        Assert.Equal("cus", meDemoted.GetProperty("role").GetString());
    }

    private async Task<Guid> NewCustomer(int credits = 10)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = new User
        {
            Email = $"{Guid.NewGuid():N}@example.test",
            FullName = "Test User",
            Role = UserRole.Customer,
            FreeTrialRemaining = credits
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
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
