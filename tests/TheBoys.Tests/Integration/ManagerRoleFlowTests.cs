using System.Reflection;
using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TheBoys.Application.Defects.DTOs;
using TheBoys.Application.Payments.DTOs;
using TheBoys.WebApi.Controllers.v2;
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

    [Fact]
    public async Task AC07_ManagerOperationalV2QueuesAndRefundPermissionGuards()
    {
        using var manager = await factory.Client(factory.ManagerId);
        using var customer = await factory.Client(factory.CustomerId);
        using var staff = await factory.Client(factory.StaffId);

        // 1. Staff v2 queues: Manager has access (200 OK), Customer is Forbidden (403)
        var jobsRes = await manager.GetAsync("/api/v2/staff/generation-jobs");
        Assert.Equal(HttpStatusCode.OK, jobsRes.StatusCode);

        var ordersRes = await manager.GetAsync("/api/v2/staff/orders");
        Assert.Equal(HttpStatusCode.OK, ordersRes.StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/v2/staff/generation-jobs")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/v2/staff/orders")).StatusCode);

        // 2. Operational v2 endpoints: Manager can read partner labs & order fulfillment
        var partnerLabsRes = await manager.GetAsync("/api/v2/partner-labs");
        Assert.Equal(HttpStatusCode.OK, partnerLabsRes.StatusCode);

        // Seed a delivered order for testing defect & refund flows
        var testAmount = 390000m;
        Order order;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            order = new Order
            {
                Id = Guid.NewGuid(),
                OrderCode = $"TB-MGR-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}",
                UserId = factory.CustomerId,
                QuoteId = null,
                PricingPackageId = DatabaseInitializer.DemoPackageId,
                Status = OrderStatus.Delivered,
                FulfillmentStatus = "Delivered",
                OrderPaymentStatus = "Paid",
                Currency = "VND",
                Quantity = 1,
                UnitPriceSnapshot = testAmount,
                PartnerCostSnapshot = 120000m,
                SubTotalVnd = testAmount,
                ShippingFeeRaw = 0,
                FinalShippingFee = 0,
                TotalAmount = testAmount,
                ReceiverName = "Nguyen Van Customer",
                ReceiverPhone = "0912345678",
                StreetAddress = "789 Dien Bien Phu",
                District = "Binh Thanh",
                City = "Ho Chi Minh",
                DeliveredAtUtc = DateTime.UtcNow,
                CreatedAtUtc = DateTime.UtcNow
            };
            db.Orders.Add(order);

            var attempt = PaymentAttempt.Create(order.Id, "Sandbox", $"REF-MGR-{Guid.NewGuid():N}", testAmount, "VND", DateTime.UtcNow.AddMinutes(30), DateTime.UtcNow);
            attempt.MarkSucceeded($"TX-MGR-{Guid.NewGuid():N}", DateTime.UtcNow);
            db.PaymentAttempts.Add(attempt);

            var lab = new PartnerLab
            {
                Id = Guid.NewGuid(),
                LabName = "Saigon Print Lab",
                ContactPerson = "Tran Van Lab",
                PhoneNumber = "0988776655",
                District = "Binh Thanh",
                Address = "12 D2 Street",
                CapacityPerDay = 40,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            db.PartnerLabs.Add(lab);

            var assignment = PartnerAssignment.Create(order.Id, lab.Id, factory.StaffId, 120000m, DateTime.UtcNow.AddDays(2), "Initial Assignment", DateTime.UtcNow);
            assignment.Accept("Accepted");
            db.PartnerAssignments.Add(assignment);

            await db.SaveChangesAsync();
        }

        // Manager can view cross-owner order v2 details & fulfillment timeline
        var getOrderRes = await manager.GetAsync($"/api/v2/orders/{order.Id}");
        Assert.Equal(HttpStatusCode.OK, getOrderRes.StatusCode);

        var timelineRes = await manager.GetAsync($"/api/v2/orders/{order.Id}/fulfillment");
        Assert.Equal(HttpStatusCode.OK, timelineRes.StatusCode);

        // Customer creates DefectReport
        var defectReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v2/orders/{order.Id}/defect-reports")
        {
            Content = JsonContent.Create(new
            {
                reason = "Sản phẩm bị nứt đế",
                evidence = new[]
                {
                    new { storageKey = "s3://evidence/crack.jpg", sha256Hex = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855" }
                }
            })
        };
        defectReq.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var defectRes = await customer.SendAsync(defectReq);
        Assert.Equal(HttpStatusCode.Created, defectRes.StatusCode);
        var defectDto = await defectRes.Content.ReadFromJsonAsync<DefectReportDto>();
        Assert.NotNull(defectDto);

        // 3. FINANCIAL GUARD: Manager CANNOT initiate refund (403 Forbidden)
        var refundReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v2/orders/{order.Id}/refunds")
        {
            Content = JsonContent.Create(new
            {
                amountVnd = testAmount,
                reason = "Manager refund attempt"
            })
        };
        refundReq.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var refundRes = await manager.SendAsync(refundReq);
        Assert.Equal(HttpStatusCode.Forbidden, refundRes.StatusCode);

        // Verify in DB: No refund created, order payment status still "Paid"
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var hasRefund = await db.Refunds.AnyAsync(r => r.PaymentAttempt.OrderId == order.Id);
            Assert.False(hasRefund);
            var dbOrder = await db.Orders.FindAsync(order.Id);
            Assert.Equal("Paid", dbOrder!.OrderPaymentStatus);
        }

        // 4. FINANCIAL GUARD: Manager CANNOT approve defect refund (RefundApproved -> 403 Forbidden)
        var resolveRefundReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v2/defect-reports/{defectDto.Id}/resolve")
        {
            Content = JsonContent.Create(new
            {
                resolutionType = "RefundApproved",
                resolutionNote = "Manager cannot approve refund"
            })
        };
        var resolveRefundRes = await manager.SendAsync(resolveRefundReq);
        Assert.Equal(HttpStatusCode.Forbidden, resolveRefundRes.StatusCode);

        // Verify defect report still Open
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var dbReport = await db.DefectReports.FindAsync(defectDto.Id);
            Assert.Equal(DefectReportStatus.Open, dbReport!.Status);
        }

        // 5. OPERATIONAL RESOLUTION: Manager CAN resolve defect with ReprintApproved (200 OK)
        var resolveReprintReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v2/defect-reports/{defectDto.Id}/resolve")
        {
            Content = JsonContent.Create(new
            {
                resolutionType = "ReprintApproved",
                resolutionNote = "Manager approved operational reprint"
            })
        };
        var resolveReprintRes = await manager.SendAsync(resolveReprintReq);
        Assert.Equal(HttpStatusCode.OK, resolveReprintRes.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var dbReport = await db.DefectReports.FindAsync(defectDto.Id);
            Assert.Equal(DefectReportStatus.ReprintApproved, dbReport!.Status);
            Assert.NotNull(dbReport.ReprintRunId);
        }

        // 6. BASELINE INTACT: Staff CAN execute defect refund and initiate refund
        Order staffOrder;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            staffOrder = new Order
            {
                Id = Guid.NewGuid(),
                OrderCode = $"TB-STF-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}",
                UserId = factory.CustomerId,
                QuoteId = null,
                PricingPackageId = DatabaseInitializer.DemoPackageId,
                Status = OrderStatus.Delivered,
                FulfillmentStatus = "Delivered",
                OrderPaymentStatus = "Paid",
                Currency = "VND",
                Quantity = 1,
                UnitPriceSnapshot = testAmount,
                PartnerCostSnapshot = 120000m,
                SubTotalVnd = testAmount,
                ShippingFeeRaw = 0,
                FinalShippingFee = 0,
                TotalAmount = testAmount,
                ReceiverName = "Nguyen Van Customer",
                ReceiverPhone = "0912345678",
                StreetAddress = "789 Dien Bien Phu",
                District = "Binh Thanh",
                City = "Ho Chi Minh",
                DeliveredAtUtc = DateTime.UtcNow,
                CreatedAtUtc = DateTime.UtcNow
            };
            db.Orders.Add(staffOrder);

            var attempt = PaymentAttempt.Create(staffOrder.Id, "Sandbox", $"REF-STF-{Guid.NewGuid():N}", testAmount, "VND", DateTime.UtcNow.AddMinutes(30), DateTime.UtcNow);
            attempt.MarkSucceeded($"TX-STF-{Guid.NewGuid():N}", DateTime.UtcNow);
            db.PaymentAttempts.Add(attempt);

            await db.SaveChangesAsync();
        }

        var staffDefectReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v2/orders/{staffOrder.Id}/defect-reports")
        {
            Content = JsonContent.Create(new
            {
                reason = "Sản phẩm hỏng hoàn toàn",
                evidence = new[]
                {
                    new { storageKey = "s3://evidence/broken.jpg", sha256Hex = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855" }
                }
            })
        };
        staffDefectReq.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var staffDefectRes = await customer.SendAsync(staffDefectReq);
        var staffDefectDto = await staffDefectRes.Content.ReadFromJsonAsync<DefectReportDto>();

        // Staff approves refund -> 200 OK
        var staffResolveRes = await staff.PostAsJsonAsync($"/api/v2/defect-reports/{staffDefectDto!.Id}/resolve", new
        {
            resolutionType = "RefundApproved",
            resolutionNote = "Staff approved full refund"
        });
        Assert.Equal(HttpStatusCode.OK, staffResolveRes.StatusCode);

        // Staff initiates refund -> 201 Created
        var staffRefundReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v2/orders/{staffOrder.Id}/refunds")
        {
            Content = JsonContent.Create(new
            {
                amountVnd = testAmount,
                reason = "Staff processed refund"
            })
        };
        staffRefundReq.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var staffRefundRes = await staff.SendAsync(staffRefundReq);
        Assert.Equal(HttpStatusCode.Created, staffRefundRes.StatusCode);
    }

    [Fact]
    public async Task AC08_ManagerPaidOrderCancellationAndReviewGuards()
    {
        using var customer = await factory.Client(factory.CustomerId);
        using var manager = await factory.Client(factory.ManagerId);
        using var staff = await factory.Client(factory.StaffId);

        var testAmount = 300000m;

        // 1. Setup: Customer has a paid order in "Printing" fulfillment status
        Order paidOrder;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            paidOrder = new Order
            {
                Id = Guid.NewGuid(),
                OrderCode = $"TB-PD-{Guid.NewGuid():N}",
                UserId = factory.CustomerId,
                QuoteId = null,
                PricingPackageId = DatabaseInitializer.DemoPackageId,
                Status = OrderStatus.Printing,
                FulfillmentStatus = "Printing",
                OrderPaymentStatus = "Paid",
                Currency = "VND",
                Quantity = 1,
                UnitPriceSnapshot = testAmount,
                PartnerCostSnapshot = 120000m,
                SubTotalVnd = testAmount,
                ShippingFeeRaw = 0,
                FinalShippingFee = 0,
                TotalAmount = testAmount,
                ReceiverName = "Paid Customer",
                ReceiverPhone = "0901234567",
                StreetAddress = "123 Le Loi",
                District = "District 1",
                City = "Ho Chi Minh",
                PrintingStartedAtUtc = DateTime.UtcNow,
                CreatedAtUtc = DateTime.UtcNow
            };
            db.Orders.Add(paidOrder);

            var attempt = PaymentAttempt.Create(paidOrder.Id, "Sandbox", $"REF-PAID-{Guid.NewGuid():N}", testAmount, "VND", DateTime.UtcNow.AddMinutes(30), DateTime.UtcNow);
            attempt.MarkSucceeded($"TX-PAID-{Guid.NewGuid():N}", DateTime.UtcNow);
            db.PaymentAttempts.Add(attempt);

            await db.SaveChangesAsync();
        }

        // SCENARIO 1: Manager calls POST /api/v2/orders/{id}/cancel on another user's paid order
        // Expected: 403 Forbidden before any mutation
        var mgrCancelPaidRes = await manager.PostAsJsonAsync($"/api/v2/orders/{paidOrder.Id}/cancel", new
        {
            reason = "Manager attempting unauthorized paid cancellation",
            decisionNote = "Manager note"
        });
        Assert.Equal(HttpStatusCode.Forbidden, mgrCancelPaidRes.StatusCode);

        // SQL Verification: No status change, no refund pending, no history, no outbox
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var dbOrder = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == paidOrder.Id);
            Assert.Equal("Paid", dbOrder.OrderPaymentStatus);
            Assert.Equal("Printing", dbOrder.FulfillmentStatus);
            Assert.Equal(OrderStatus.Printing, dbOrder.Status);
            Assert.Null(dbOrder.CancelledAtUtc);

            var requests = await db.OrderCancellationRequests.AsNoTracking().Where(c => c.OrderId == paidOrder.Id).ToListAsync();
            Assert.Empty(requests);

            var histories = await db.OrderStatusHistories.AsNoTracking()
                .Where(h => h.OrderId == paidOrder.Id && h.Axis == "Payment")
                .ToListAsync();
            Assert.Empty(histories);

            var outboxMessages = await db.OutboxMessages.AsNoTracking()
                .Where(m => m.AggregateId == paidOrder.Id && (m.EventType == "OrderCancelled" || m.EventType == "OrderCancellationApproved"))
                .ToListAsync();
            Assert.Empty(outboxMessages);
        }

        // 2. Setup: Customer requests cancellation on an eligible paid order (e.g. before production)
        Order customerAwaitingOrder;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            customerAwaitingOrder = new Order
            {
                Id = Guid.NewGuid(),
                OrderCode = $"TB-AW-{Guid.NewGuid():N}",
                UserId = factory.CustomerId,
                QuoteId = null,
                PricingPackageId = DatabaseInitializer.DemoPackageId,
                Status = OrderStatus.Draft,
                FulfillmentStatus = "AwaitingAssignment",
                OrderPaymentStatus = "Paid",
                Currency = "VND",
                Quantity = 1,
                UnitPriceSnapshot = testAmount,
                PartnerCostSnapshot = 120000m,
                SubTotalVnd = testAmount,
                ShippingFeeRaw = 0,
                FinalShippingFee = 0,
                TotalAmount = testAmount,
                ReceiverName = "Paid Customer",
                ReceiverPhone = "0901234567",
                StreetAddress = "123 Le Loi",
                District = "District 1",
                City = "Ho Chi Minh",
                CreatedAtUtc = DateTime.UtcNow
            };
            db.Orders.Add(customerAwaitingOrder);

            var attempt = PaymentAttempt.Create(customerAwaitingOrder.Id, "Sandbox", $"REF-AWAIT-{Guid.NewGuid():N}", testAmount, "VND", DateTime.UtcNow.AddMinutes(30), DateTime.UtcNow);
            attempt.MarkSucceeded($"TX-AWAIT-{Guid.NewGuid():N}", DateTime.UtcNow);
            db.PaymentAttempts.Add(attempt);

            await db.SaveChangesAsync();
        }

        // Customer requests cancellation -> returns 200 OK with PendingReview
        var cusCancelRes = await customer.PostAsJsonAsync($"/api/v2/orders/{customerAwaitingOrder.Id}/cancel", new
        {
            reason = "Customer changed mind before printing begins"
        });
        Assert.Equal(HttpStatusCode.OK, cusCancelRes.StatusCode);
        var cusCancelJson = await Json(cusCancelRes);
        Assert.Equal("PendingReview", cusCancelJson.GetProperty("cancellationStatus").GetString());
        var cancellationRequestId = cusCancelJson.GetProperty("cancellationRequestId").GetGuid();

        // SCENARIO 2: Manager calls POST /api/v2/orders/cancellation-requests/{id}/review with Approved = true
        // Expected: 403 Forbidden
        var mgrReviewRes = await manager.PostAsJsonAsync($"/api/v2/orders/cancellation-requests/{cancellationRequestId}/review", new
        {
            approved = true,
            decisionNote = "Manager trying to approve refund"
        });
        Assert.Equal(HttpStatusCode.Forbidden, mgrReviewRes.StatusCode);

        // SQL Verification: Request remains Pending, payment status still Paid, fulfillment unchanged
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var cancelReq = await db.OrderCancellationRequests.AsNoTracking().SingleAsync(c => c.Id == cancellationRequestId);
            Assert.Equal(CancellationRequestStatus.Pending, cancelReq.Status);

            var dbOrder = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == customerAwaitingOrder.Id);
            Assert.Equal("Paid", dbOrder.OrderPaymentStatus);
            Assert.Equal("AwaitingAssignment", dbOrder.FulfillmentStatus);

            var paymentHistories = await db.OrderStatusHistories.AsNoTracking()
                .Where(h => h.OrderId == customerAwaitingOrder.Id && h.Axis == "Payment")
                .ToListAsync();
            Assert.Empty(paymentHistories);

            var approvedOutbox = await db.OutboxMessages.AsNoTracking()
                .Where(m => m.AggregateId == customerAwaitingOrder.Id && m.EventType == "OrderCancellationApproved")
                .ToListAsync();
            Assert.Empty(approvedOutbox);
        }

        // SCENARIO 3: Manager operational capability - Manager CAN cancel an unpaid order
        Order unpaidOrder;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            unpaidOrder = new Order
            {
                Id = Guid.NewGuid(),
                OrderCode = $"TB-UP-{Guid.NewGuid():N}",
                UserId = factory.CustomerId,
                QuoteId = null,
                PricingPackageId = DatabaseInitializer.DemoPackageId,
                Status = OrderStatus.Draft,
                FulfillmentStatus = "PendingPayment",
                OrderPaymentStatus = "Unpaid",
                Currency = "VND",
                Quantity = 1,
                UnitPriceSnapshot = testAmount,
                PartnerCostSnapshot = 120000m,
                SubTotalVnd = testAmount,
                ShippingFeeRaw = 0,
                FinalShippingFee = 0,
                TotalAmount = testAmount,
                ReceiverName = "Unpaid Customer",
                ReceiverPhone = "0901234567",
                StreetAddress = "123 Le Loi",
                District = "District 1",
                City = "Ho Chi Minh",
                CreatedAtUtc = DateTime.UtcNow
            };
            db.Orders.Add(unpaidOrder);
            await db.SaveChangesAsync();
        }

        var mgrCancelUnpaidRes = await manager.PostAsJsonAsync($"/api/v2/orders/{unpaidOrder.Id}/cancel", new
        {
            reason = "Manager operational cleanup of abandoned unpaid order"
        });
        Assert.Equal(HttpStatusCode.OK, mgrCancelUnpaidRes.StatusCode);
        var unpaidJson = await Json(mgrCancelUnpaidRes);
        Assert.Equal("CancelledDirectly", unpaidJson.GetProperty("cancellationStatus").GetString());
        Assert.False(unpaidJson.GetProperty("refundRequired").GetBoolean());

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var dbOrder = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == unpaidOrder.Id);
            Assert.Equal("Cancelled", dbOrder.FulfillmentStatus);
            Assert.Equal("Unpaid", dbOrder.OrderPaymentStatus);
            Assert.Equal(OrderStatus.Cancelled, dbOrder.Status);
        }

        // SCENARIO 4: Manager cancelling own personal paid order submits Pending request without auto-approving
        Order managerOwnOrder;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            managerOwnOrder = new Order
            {
                Id = Guid.NewGuid(),
                OrderCode = $"TB-MO-{Guid.NewGuid():N}",
                UserId = factory.ManagerId,
                QuoteId = null,
                PricingPackageId = DatabaseInitializer.DemoPackageId,
                Status = OrderStatus.Draft,
                FulfillmentStatus = "AwaitingAssignment",
                OrderPaymentStatus = "Paid",
                Currency = "VND",
                Quantity = 1,
                UnitPriceSnapshot = testAmount,
                PartnerCostSnapshot = 120000m,
                SubTotalVnd = testAmount,
                ShippingFeeRaw = 0,
                FinalShippingFee = 0,
                TotalAmount = testAmount,
                ReceiverName = "Manager Personal",
                ReceiverPhone = "0909998877",
                StreetAddress = "456 Nguyen Thi Minh Khai",
                District = "District 3",
                City = "Ho Chi Minh",
                CreatedAtUtc = DateTime.UtcNow
            };
            db.Orders.Add(managerOwnOrder);

            var attempt = PaymentAttempt.Create(managerOwnOrder.Id, "Sandbox", $"REF-MGR-{Guid.NewGuid():N}", testAmount, "VND", DateTime.UtcNow.AddMinutes(30), DateTime.UtcNow);
            attempt.MarkSucceeded($"TX-MGR-{Guid.NewGuid():N}", DateTime.UtcNow);
            db.PaymentAttempts.Add(attempt);

            await db.SaveChangesAsync();
        }

        var mgrOwnCancelRes = await manager.PostAsJsonAsync($"/api/v2/orders/{managerOwnOrder.Id}/cancel", new
        {
            reason = "Manager customer changed mind on personal order"
        });
        Assert.Equal(HttpStatusCode.OK, mgrOwnCancelRes.StatusCode);
        var mgrOwnJson = await Json(mgrOwnCancelRes);
        Assert.Equal("PendingReview", mgrOwnJson.GetProperty("cancellationStatus").GetString());
        Assert.True(mgrOwnJson.GetProperty("refundRequired").GetBoolean());

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var dbOrder = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == managerOwnOrder.Id);
            Assert.Equal("Paid", dbOrder.OrderPaymentStatus);
            Assert.Equal("AwaitingAssignment", dbOrder.FulfillmentStatus);
            var ownReq = await db.OrderCancellationRequests.AsNoTracking().SingleAsync(c => c.OrderId == managerOwnOrder.Id);
            Assert.Equal(CancellationRequestStatus.Pending, ownReq.Status);
        }

        // SCENARIO 5: Staff baseline intact - Staff CAN approve cancellation request
        var staffApproveRes = await staff.PostAsJsonAsync($"/api/v2/orders/cancellation-requests/{cancellationRequestId}/review", new
        {
            approved = true,
            decisionNote = "Staff approved customer cancellation for refund"
        });
        Assert.Equal(HttpStatusCode.OK, staffApproveRes.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var cancelReq = await db.OrderCancellationRequests.AsNoTracking().SingleAsync(c => c.Id == cancellationRequestId);
            Assert.Equal(CancellationRequestStatus.Approved, cancelReq.Status);

            var dbOrder = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == customerAwaitingOrder.Id);
            Assert.Equal("RefundPending", dbOrder.OrderPaymentStatus);
            Assert.Equal("Cancelled", dbOrder.FulfillmentStatus);
            Assert.Equal(OrderStatus.Cancelled, dbOrder.Status);
        }

        // SCENARIO 6: Deterministic race condition interleaving regression
        // Payment commits after controller dispatch and before handler reads SQL
        Order raceOrder;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            raceOrder = new Order
            {
                Id = Guid.NewGuid(),
                OrderCode = $"TB-RC-{Guid.NewGuid():N}",
                UserId = factory.CustomerId,
                QuoteId = null,
                PricingPackageId = DatabaseInitializer.DemoPackageId,
                Status = OrderStatus.Draft,
                FulfillmentStatus = "PendingPayment",
                OrderPaymentStatus = "Unpaid",
                Currency = "VND",
                Quantity = 1,
                UnitPriceSnapshot = testAmount,
                PartnerCostSnapshot = 120000m,
                SubTotalVnd = testAmount,
                ShippingFeeRaw = 0,
                FinalShippingFee = 0,
                TotalAmount = testAmount,
                ReceiverName = "Race Customer",
                ReceiverPhone = "0901234567",
                StreetAddress = "123 Le Loi",
                District = "District 1",
                City = "Ho Chi Minh",
                CreatedAtUtc = DateTime.UtcNow
            };
            db.Orders.Add(raceOrder);
            await db.SaveChangesAsync();
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var sender = DispatchProxy.Create<ISender, PaymentInterleavingProxy>();
            var proxy = (PaymentInterleavingProxy)(object)sender;
            proxy.Inner = scope.ServiceProvider.GetRequiredService<ISender>();
            proxy.Db = db;
            proxy.OrderId = raceOrder.Id;

            var controller = new OrdersV2Controller(sender, db)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                        {
                            new Claim(ClaimTypes.NameIdentifier, factory.ManagerId.ToString()),
                            new Claim("sub", factory.ManagerId.ToString()),
                            new Claim(ClaimTypes.Role, Roles.Manager),
                            new Claim("role", Roles.Manager)
                        }, "test"))
                    }
                }
            };

            var cancelActionRes = await controller.Cancel(raceOrder.Id, new CancelOrderV2ApiRequest
            {
                Reason = "Manager attempting race cancellation",
                DecisionNote = "Race attempt"
            }, CancellationToken.None);

            Assert.IsType<ForbidResult>(cancelActionRes);
            Assert.True(proxy.DidInterleave, "Interleaving proxy should have committed payment before handler execution");

            db.ChangeTracker.Clear();
            var persisted = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == raceOrder.Id);
            Assert.Equal("Paid", persisted.OrderPaymentStatus);
            Assert.Equal("AwaitingProduction", persisted.FulfillmentStatus);
            Assert.NotEqual(OrderStatus.Cancelled, persisted.Status);

            var approvedRequests = await db.OrderCancellationRequests.AsNoTracking()
                .CountAsync(c => c.OrderId == raceOrder.Id && c.Status == CancellationRequestStatus.Approved);
            Assert.Equal(0, approvedRequests);

            var paymentHistories = await db.OrderStatusHistories.AsNoTracking()
                .Where(h => h.OrderId == raceOrder.Id && h.Axis == "Payment")
                .ToListAsync();
            Assert.Empty(paymentHistories);

            var cancelOutbox = await db.OutboxMessages.AsNoTracking()
                .Where(m => m.AggregateId == raceOrder.Id && (m.EventType == "OrderCancelled" || m.EventType == "OrderCancellationApproved"))
                .ToListAsync();
            Assert.Empty(cancelOutbox);
        }
    }

    [Fact]
    public async Task AC09_ManagerRefundReconciliationGuardsAgainstManualConfirmation()
    {
        using var manager = await factory.Client(factory.ManagerId);
        using var staff = await factory.Client(factory.StaffId);
        using var admin = await factory.Client(factory.AdminId);
        using var customer = await factory.Client(factory.CustomerId);

        var testAmount = 450000m;

        // SCENARIO 1: Manager calling Reconcile on offline unsupported gateway (PayOS/SePay/Simulated Offline) -> 403 Forbidden & Zero Mutations
        Order offlineOrder;
        Refund offlineRefund;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            offlineOrder = new Order
            {
                Id = Guid.NewGuid(),
                OrderCode = $"TB-OFF-{Guid.NewGuid():N}",
                UserId = factory.CustomerId,
                QuoteId = null,
                PricingPackageId = DatabaseInitializer.DemoPackageId,
                Status = OrderStatus.Cancelled,
                FulfillmentStatus = "Cancelled",
                OrderPaymentStatus = "NeedsAttention",
                Currency = "VND",
                Quantity = 1,
                UnitPriceSnapshot = testAmount,
                PartnerCostSnapshot = 120000m,
                SubTotalVnd = testAmount,
                ShippingFeeRaw = 0,
                FinalShippingFee = 0,
                TotalAmount = testAmount,
                ReceiverName = "Offline Customer",
                ReceiverPhone = "0901234567",
                StreetAddress = "123 Le Loi",
                District = "District 1",
                City = "Ho Chi Minh",
                CreatedAtUtc = DateTime.UtcNow
            };
            db.Orders.Add(offlineOrder);

            var attempt = PaymentAttempt.Create(offlineOrder.Id, "Sandbox", $"REF-OFFLINE-SIMULATE_OFFLINE_UNSUPPORTED-{Guid.NewGuid():N}", testAmount, "VND", DateTime.UtcNow.AddMinutes(30), DateTime.UtcNow);
            attempt.MarkSucceeded($"TX-OFF-{Guid.NewGuid():N}", DateTime.UtcNow);
            db.PaymentAttempts.Add(attempt);

            offlineRefund = Refund.Create(attempt.Id, factory.StaffId, Guid.NewGuid(), testAmount, "Offline refund test", DateTime.UtcNow);
            offlineRefund.MarkNeedsAttention("Simulated offline timeout");
            db.Refunds.Add(offlineRefund);

            var cancellation = OrderCancellationRequest.Create(offlineOrder.Id, factory.CustomerId, "Cancel for refund", DateTime.UtcNow);
            cancellation.Approve(factory.StaffId, "Approved", offlineRefund.Id, DateTime.UtcNow);
            db.OrderCancellationRequests.Add(cancellation);

            await db.SaveChangesAsync();
        }

        // Customer calling reconcile -> 403 Forbidden (Roles.Management requirement)
        var customerReconcileRes = await customer.PostAsJsonAsync($"/api/v2/refunds/{offlineRefund.Id}/reconcile", new { note = "Customer attempt" });
        Assert.Equal(HttpStatusCode.Forbidden, customerReconcileRes.StatusCode);

        // Manager calling reconcile on offline gateway -> 403 Forbidden (Cannot perform manual financial confirmation)
        var mgrOfflineRes = await manager.PostAsJsonAsync($"/api/v2/refunds/{offlineRefund.Id}/reconcile", new { note = "Manager manual attestation attempt" });
        Assert.Equal(HttpStatusCode.Forbidden, mgrOfflineRes.StatusCode);

        // Verify in DB: No financial side effects, no status change, no audit, no outbox
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var persistedRefund = await db.Refunds.AsNoTracking().SingleAsync(r => r.Id == offlineRefund.Id);
            Assert.Equal(RefundStatus.NeedsAttention, persistedRefund.Status);
            Assert.Null(persistedRefund.CompletedAtUtc);
            Assert.Null(persistedRefund.ProviderReference);

            var persistedOrder = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == offlineOrder.Id);
            Assert.Equal("NeedsAttention", persistedOrder.OrderPaymentStatus);

            var histories = await db.OrderStatusHistories.AsNoTracking()
                .Where(h => h.OrderId == offlineOrder.Id && h.Reason.Contains("Manager manual attestation"))
                .ToListAsync();
            Assert.Empty(histories);

            var audits = await db.AuditLogs.AsNoTracking()
                .Where(a => a.AggregateId == offlineRefund.Id && a.Action == "ReconcileRefundSucceeded")
                .ToListAsync();
            Assert.Empty(audits);

            var outboxes = await db.OutboxMessages.AsNoTracking()
                .Where(m => m.AggregateId == offlineOrder.Id && m.EventType == "RefundReconciledSucceeded")
                .ToListAsync();
            Assert.Empty(outboxes);
        }

        // SCENARIO 2: Staff calling Reconcile on offline gateway -> 200 OK & Manual Confirmation Succeeded
        var staffOfflineRes = await staff.PostAsJsonAsync($"/api/v2/refunds/{offlineRefund.Id}/reconcile", new { note = "Staff manual bank transfer confirmed" });
        Assert.Equal(HttpStatusCode.OK, staffOfflineRes.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var persistedRefund = await db.Refunds.AsNoTracking().SingleAsync(r => r.Id == offlineRefund.Id);
            Assert.Equal(RefundStatus.Succeeded, persistedRefund.Status);
            Assert.NotNull(persistedRefund.CompletedAtUtc);
            Assert.NotNull(persistedRefund.ProviderReference);
            Assert.StartsWith("MANUAL-REF-", persistedRefund.ProviderReference);

            var persistedOrder = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == offlineOrder.Id);
            Assert.Equal("Refunded", persistedOrder.OrderPaymentStatus);

            var cancellation = await db.OrderCancellationRequests.AsNoTracking().SingleAsync(c => c.RefundId == offlineRefund.Id);
            Assert.Equal(CancellationRequestStatus.Completed, cancellation.Status);

            var history = await db.OrderStatusHistories.AsNoTracking()
                .SingleOrDefaultAsync(h => h.OrderId == offlineOrder.Id && h.Axis == "Payment" && h.ToStatus == "Refunded");
            Assert.NotNull(history);
            Assert.Equal(factory.StaffId, history.ActorUserId);

            var audit = await db.AuditLogs.AsNoTracking()
                .SingleOrDefaultAsync(a => a.AggregateId == offlineRefund.Id && a.Action == "ReconcileRefundSucceeded");
            Assert.NotNull(audit);

            var outbox = await db.OutboxMessages.AsNoTracking()
                .SingleOrDefaultAsync(m => m.AggregateId == offlineOrder.Id && m.EventType == "RefundReconciledSucceeded");
            Assert.NotNull(outbox);
        }

        // SCENARIO 3: Admin calling Reconcile on offline gateway -> 200 OK & Manual Confirmation Succeeded
        Order adminOfflineOrder;
        Refund adminOfflineRefund;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            adminOfflineOrder = new Order
            {
                Id = Guid.NewGuid(),
                OrderCode = $"TB-ADM-OFF-{Guid.NewGuid():N}",
                UserId = factory.CustomerId,
                QuoteId = null,
                PricingPackageId = DatabaseInitializer.DemoPackageId,
                Status = OrderStatus.Cancelled,
                FulfillmentStatus = "Cancelled",
                OrderPaymentStatus = "NeedsAttention",
                Currency = "VND",
                Quantity = 1,
                UnitPriceSnapshot = testAmount,
                PartnerCostSnapshot = 120000m,
                SubTotalVnd = testAmount,
                ShippingFeeRaw = 0,
                FinalShippingFee = 0,
                TotalAmount = testAmount,
                ReceiverName = "Admin Offline Customer",
                ReceiverPhone = "0901234567",
                StreetAddress = "123 Le Loi",
                District = "District 1",
                City = "Ho Chi Minh",
                CreatedAtUtc = DateTime.UtcNow
            };
            db.Orders.Add(adminOfflineOrder);

            var attempt = PaymentAttempt.Create(adminOfflineOrder.Id, "Sandbox", $"REF-ADM-SIMULATE_OFFLINE_UNSUPPORTED-{Guid.NewGuid():N}", testAmount, "VND", DateTime.UtcNow.AddMinutes(30), DateTime.UtcNow);
            attempt.MarkSucceeded($"TX-ADM-OFF-{Guid.NewGuid():N}", DateTime.UtcNow);
            db.PaymentAttempts.Add(attempt);

            adminOfflineRefund = Refund.Create(attempt.Id, factory.StaffId, Guid.NewGuid(), testAmount, "Admin offline refund test", DateTime.UtcNow);
            adminOfflineRefund.MarkNeedsAttention("Simulated offline timeout");
            db.Refunds.Add(adminOfflineRefund);

            await db.SaveChangesAsync();
        }

        var adminOfflineRes = await admin.PostAsJsonAsync($"/api/v2/refunds/{adminOfflineRefund.Id}/reconcile", new { note = "Admin manual bank transfer confirmed" });
        Assert.Equal(HttpStatusCode.OK, adminOfflineRes.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var persistedRefund = await db.Refunds.AsNoTracking().SingleAsync(r => r.Id == adminOfflineRefund.Id);
            Assert.Equal(RefundStatus.Succeeded, persistedRefund.Status);
            Assert.StartsWith("MANUAL-REF-", persistedRefund.ProviderReference);

            var persistedOrder = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == adminOfflineOrder.Id);
            Assert.Equal("Refunded", persistedOrder.OrderPaymentStatus);
        }

        // SCENARIO 4: Manager calling Reconcile with verified gateway evidence (Succeeded) -> 200 OK
        Order onlineOrder;
        Refund onlineRefund;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            onlineOrder = new Order
            {
                Id = Guid.NewGuid(),
                OrderCode = $"TB-ONL-{Guid.NewGuid():N}",
                UserId = factory.CustomerId,
                QuoteId = null,
                PricingPackageId = DatabaseInitializer.DemoPackageId,
                Status = OrderStatus.Cancelled,
                FulfillmentStatus = "Cancelled",
                OrderPaymentStatus = "NeedsAttention",
                Currency = "VND",
                Quantity = 1,
                UnitPriceSnapshot = testAmount,
                PartnerCostSnapshot = 120000m,
                SubTotalVnd = testAmount,
                ShippingFeeRaw = 0,
                FinalShippingFee = 0,
                TotalAmount = testAmount,
                ReceiverName = "Online Customer",
                ReceiverPhone = "0901234567",
                StreetAddress = "123 Le Loi",
                District = "District 1",
                City = "Ho Chi Minh",
                CreatedAtUtc = DateTime.UtcNow
            };
            db.Orders.Add(onlineOrder);

            var attempt = PaymentAttempt.Create(onlineOrder.Id, "Sandbox", $"REF-ONLINE-{Guid.NewGuid():N}", testAmount, "VND", DateTime.UtcNow.AddMinutes(30), DateTime.UtcNow);
            attempt.MarkSucceeded($"TX-ONL-{Guid.NewGuid():N}", DateTime.UtcNow);
            db.PaymentAttempts.Add(attempt);

            onlineRefund = Refund.Create(attempt.Id, factory.StaffId, Guid.NewGuid(), testAmount, "Online refund test", DateTime.UtcNow);
            onlineRefund.MarkNeedsAttention("Simulated online timeout");
            db.Refunds.Add(onlineRefund);

            await db.SaveChangesAsync();
        }

        var mgrOnlineRes = await manager.PostAsJsonAsync($"/api/v2/refunds/{onlineRefund.Id}/reconcile", new { note = "Manager verified gateway evidence reconcile" });
        Assert.Equal(HttpStatusCode.OK, mgrOnlineRes.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var persistedRefund = await db.Refunds.AsNoTracking().SingleAsync(r => r.Id == onlineRefund.Id);
            Assert.Equal(RefundStatus.Succeeded, persistedRefund.Status);
            Assert.StartsWith("SANDBOX-REF-", persistedRefund.ProviderReference);

            var persistedOrder = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == onlineOrder.Id);
            Assert.Equal("Refunded", persistedOrder.OrderPaymentStatus);
        }

        // SCENARIO 5: Manager calling Reconcile with verified gateway evidence (Failed) -> 200 OK & status marked Failed
        Order failOrder;
        Refund failRefund;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            failOrder = new Order
            {
                Id = Guid.NewGuid(),
                OrderCode = $"TB-FAIL-{Guid.NewGuid():N}",
                UserId = factory.CustomerId,
                QuoteId = null,
                PricingPackageId = DatabaseInitializer.DemoPackageId,
                Status = OrderStatus.Cancelled,
                FulfillmentStatus = "Cancelled",
                OrderPaymentStatus = "NeedsAttention",
                Currency = "VND",
                Quantity = 1,
                UnitPriceSnapshot = testAmount,
                PartnerCostSnapshot = 120000m,
                SubTotalVnd = testAmount,
                ShippingFeeRaw = 0,
                FinalShippingFee = 0,
                TotalAmount = testAmount,
                ReceiverName = "Fail Customer",
                ReceiverPhone = "0901234567",
                StreetAddress = "123 Le Loi",
                District = "District 1",
                City = "Ho Chi Minh",
                CreatedAtUtc = DateTime.UtcNow
            };
            db.Orders.Add(failOrder);

            var attempt = PaymentAttempt.Create(failOrder.Id, "Sandbox", $"REF-FAIL-SIMULATE_REFUND_FAIL-{Guid.NewGuid():N}", testAmount, "VND", DateTime.UtcNow.AddMinutes(30), DateTime.UtcNow);
            attempt.MarkSucceeded($"TX-FAIL-{Guid.NewGuid():N}", DateTime.UtcNow);
            db.PaymentAttempts.Add(attempt);

            failRefund = Refund.Create(attempt.Id, factory.StaffId, Guid.NewGuid(), testAmount, "Fail refund test", DateTime.UtcNow);
            failRefund.MarkNeedsAttention("Simulated failure timeout");
            db.Refunds.Add(failRefund);

            await db.SaveChangesAsync();
        }

        var mgrFailRes = await manager.PostAsJsonAsync($"/api/v2/refunds/{failRefund.Id}/reconcile", new { note = "Manager verified gateway failure reconcile" });
        Assert.Equal(HttpStatusCode.OK, mgrFailRes.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var persistedRefund = await db.Refunds.AsNoTracking().SingleAsync(r => r.Id == failRefund.Id);
            Assert.Equal(RefundStatus.Failed, persistedRefund.Status);

            var persistedOrder = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == failOrder.Id);
            Assert.Equal("RefundPending", persistedOrder.OrderPaymentStatus);
        }
    }


    private async Task<Guid> NewCustomer(int credits = 10)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = DateTime.UtcNow;
        var email = $"{Guid.NewGuid():N}@example.test";
        var user = new User
        {
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            FullName = "Test User",
            Role = UserRole.Customer,
            AccountStatus = AccountStatus.Active,
            EmailVerifiedAtUtc = now,
            FreeTrialRemaining = credits
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var account = CreditAccount.Create(user.Id, trialBalance: credits, purchasedBalance: 0);
        db.CreditAccounts.Add(account);

        var ledgerEntry = CreditLedgerEntry.CreateOpeningBalance(
            user.Id,
            CreditBucket.Trial,
            credits,
            "SEEDED",
            "Test seed balance",
            now);
        db.CreditLedger.Add(ledgerEntry);

        var consent = ConsentRecord.Record(user.Id, "AiModelProcessing", "v1.0", true, now);
        db.ConsentRecords.Add(consent);

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

public class PaymentInterleavingProxy : DispatchProxy
{
    public ISender Inner = null!;
    public ApplicationDbContext Db = null!;
    public Guid OrderId;
    public bool DidInterleave;

    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (args?.FirstOrDefault() is TheBoys.Application.Orders.Commands.CancelOrderV2.CancelOrderV2Command)
        {
            Db.Orders.Where(o => o.Id == OrderId).ExecuteUpdate(s =>
                s.SetProperty(o => o.OrderPaymentStatus, "Paid")
                 .SetProperty(o => o.FulfillmentStatus, "AwaitingProduction"));
            DidInterleave = true;
        }
        return method!.Invoke(Inner, args);
    }
}
