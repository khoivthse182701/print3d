using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;
using TheBoys.Infrastructure.Persistence;
using TheBoys.WebApi;

namespace TheBoys.Tests.Integration;

public class StaffQueueTests(DemoFactory factory) : IClassFixture<DemoFactory>
{
    [Fact]
    public async Task AC01_Rbac_GenerationJobsAndOrders_RequireStaffOrAdmin()
    {
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v2/staff/generation-jobs")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v2/staff/orders")).StatusCode);

        using var customer = await factory.Client(factory.CustomerId);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/v2/staff/generation-jobs")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/v2/staff/orders")).StatusCode);

        using var staff = await factory.Client(factory.StaffId);
        Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync("/api/v2/staff/generation-jobs")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync("/api/v2/staff/orders")).StatusCode);

        using var admin = await factory.Client(factory.AdminId);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/v2/staff/generation-jobs")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/v2/staff/orders")).StatusCode);
    }

    [Fact]
    public async Task AC02_GenerationJobs_Filters_Combined_CaseInsensitiveProvider_Date_Age()
    {
        var testNow = DateTime.UtcNow;
        var tag = Guid.NewGuid().ToString("N");

        var job1Id = Guid.NewGuid();
        var job2Id = Guid.NewGuid();
        var job3Id = Guid.NewGuid();
        var job4Id = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.GenerationTasks.AddRange(
                new GenerationTask
                {
                    Id = job1Id,
                    UserId = factory.CustomerId,
                    ModelName = $"Job1_{tag}",
                    Provider = "Meshy",
                    Status = GenerationStatus.Queued,
                    ReviewStatus = PrintReviewStatus.Pending,
                    CreatedAtUtc = testNow.AddMinutes(-60)
                },
                new GenerationTask
                {
                    Id = job2Id,
                    UserId = factory.CustomerId,
                    ModelName = $"Job2_{tag}",
                    Provider = "MESHY",
                    Status = GenerationStatus.Processing,
                    ReviewStatus = PrintReviewStatus.Pending,
                    CreatedAtUtc = testNow.AddMinutes(-30)
                },
                new GenerationTask
                {
                    Id = job3Id,
                    UserId = factory.CustomerId,
                    ModelName = $"Job3_{tag}",
                    Provider = "Tripo",
                    Status = GenerationStatus.Succeeded,
                    ReviewStatus = PrintReviewStatus.Approved,
                    CreatedAtUtc = testNow.AddMinutes(-10),
                    CompletedAtUtc = testNow.AddMinutes(-5)
                },
                new GenerationTask
                {
                    Id = job4Id,
                    UserId = factory.CustomerId,
                    ModelName = $"Job4_{tag}",
                    Provider = "Meshy",
                    Status = GenerationStatus.Failed,
                    ReviewStatus = PrintReviewStatus.Rejected,
                    CreatedAtUtc = testNow.AddMinutes(-2)
                }
            );
            await db.SaveChangesAsync();
        }

        using var staff = await factory.Client(factory.StaffId);

        // 1. Filter by status
        var statusRes = await Json(await staff.GetAsync($"/api/v2/staff/generation-jobs?status=processing&pageSize=100"));
        var statusItems = statusRes.GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(statusItems, x => x.GetProperty("id").GetGuid() == job2Id);
        Assert.DoesNotContain(statusItems, x => x.GetProperty("id").GetGuid() == job1Id);

        // 2. Filter by reviewStatus
        var reviewRes = await Json(await staff.GetAsync($"/api/v2/staff/generation-jobs?reviewStatus=approved&pageSize=100"));
        var reviewItems = reviewRes.GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(reviewItems, x => x.GetProperty("id").GetGuid() == job3Id);
        Assert.DoesNotContain(reviewItems, x => x.GetProperty("id").GetGuid() == job1Id);

        // 3. Provider trim and case-insensitive exact match
        var providerRes = await Json(await staff.GetAsync($"/api/v2/staff/generation-jobs?provider=%20%20meshy%20%20&pageSize=100"));
        var providerItems = providerRes.GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(providerItems, x => x.GetProperty("id").GetGuid() == job1Id);
        Assert.Contains(providerItems, x => x.GetProperty("id").GetGuid() == job2Id);
        Assert.Contains(providerItems, x => x.GetProperty("id").GetGuid() == job4Id);
        Assert.DoesNotContain(providerItems, x => x.GetProperty("id").GetGuid() == job3Id);

        // 4. Age filter (minAgeMinutes >= 25 minutes)
        var ageRes = await Json(await staff.GetAsync($"/api/v2/staff/generation-jobs?minAgeMinutes=25&pageSize=100"));
        var ageItems = ageRes.GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(ageItems, x => x.GetProperty("id").GetGuid() == job1Id);
        Assert.Contains(ageItems, x => x.GetProperty("id").GetGuid() == job2Id);
        Assert.DoesNotContain(ageItems, x => x.GetProperty("id").GetGuid() == job3Id);
        Assert.DoesNotContain(ageItems, x => x.GetProperty("id").GetGuid() == job4Id);

        // 5. Date filter inclusive/exclusive
        var fromStr = Uri.EscapeDataString(testNow.AddMinutes(-40).ToString("yyyy-MM-ddTHH:mm:ssZ"));
        var toStr = Uri.EscapeDataString(testNow.AddMinutes(-5).ToString("yyyy-MM-ddTHH:mm:ssZ"));
        var dateRes = await Json(await staff.GetAsync($"/api/v2/staff/generation-jobs?createdFromUtc={fromStr}&createdToUtc={toStr}&pageSize=100"));
        var dateItems = dateRes.GetProperty("items").EnumerateArray().ToList();
        Assert.DoesNotContain(dateItems, x => x.GetProperty("id").GetGuid() == job1Id); // -60m is outside
        Assert.Contains(dateItems, x => x.GetProperty("id").GetGuid() == job2Id); // -30m is inside
        Assert.Contains(dateItems, x => x.GetProperty("id").GetGuid() == job3Id); // -10m is inside
        Assert.DoesNotContain(dateItems, x => x.GetProperty("id").GetGuid() == job4Id); // -2m is outside

        // 6. Combined filters
        var combinedRes = await Json(await staff.GetAsync(
            $"/api/v2/staff/generation-jobs?status=queued&provider=MESHY&minAgeMinutes=45&pageSize=100"));
        var combinedItems = combinedRes.GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(combinedItems, x => x.GetProperty("id").GetGuid() == job1Id);
        Assert.DoesNotContain(combinedItems, x => x.GetProperty("id").GetGuid() == job2Id);
    }

    [Fact]
    public async Task AC03_Orders_Filters_Status_OrderCode_Date_Age()
    {
        var testNow = DateTime.UtcNow;
        var tag = Guid.NewGuid().ToString("N")[..8];
        var code1 = $"TB-ORD-{tag}-01";
        var code2 = $"TB-ORD-{tag}-02";
        var code3 = $"TB-ORD-{tag}-03";

        var taskId = Guid.NewGuid();
        var order1Id = Guid.NewGuid();
        var order2Id = Guid.NewGuid();
        var order3Id = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.GenerationTasks.Add(new GenerationTask
            {
                Id = taskId,
                UserId = factory.CustomerId,
                ModelName = $"TaskForOrders_{tag}",
                Provider = "Meshy",
                Status = GenerationStatus.Succeeded
            });

            db.Orders.AddRange(
                new Order
                {
                    Id = order1Id,
                    OrderCode = code1,
                    UserId = factory.CustomerId,
                    GenerationTaskId = taskId,
                    PricingPackageId = DatabaseInitializer.DemoPackageId,
                    Status = OrderStatus.Draft,
                    ReceiverName = "Cus 1",
                    ReceiverPhone = "0900000001",
                    StreetAddress = "1 Street",
                    District = "D1",
                    CreatedAtUtc = testNow.AddMinutes(-50)
                },
                new Order
                {
                    Id = order2Id,
                    OrderCode = code2,
                    UserId = factory.CustomerId,
                    GenerationTaskId = taskId,
                    PricingPackageId = DatabaseInitializer.DemoPackageId,
                    Status = OrderStatus.Printing,
                    ReceiverName = "Cus 2",
                    ReceiverPhone = "0900000002",
                    StreetAddress = "2 Street",
                    District = "D2",
                    CreatedAtUtc = testNow.AddMinutes(-20),
                    PrintingStartedAtUtc = testNow.AddMinutes(-15)
                },
                new Order
                {
                    Id = order3Id,
                    OrderCode = code3,
                    UserId = factory.CustomerId,
                    GenerationTaskId = taskId,
                    PricingPackageId = DatabaseInitializer.DemoPackageId,
                    Status = OrderStatus.Delivered,
                    ReceiverName = "Cus 3",
                    ReceiverPhone = "0900000003",
                    StreetAddress = "3 Street",
                    District = "D3",
                    CreatedAtUtc = testNow.AddMinutes(-5),
                    DeliveredAtUtc = testNow.AddMinutes(-1)
                }
            );
            await db.SaveChangesAsync();
        }

        using var staff = await factory.Client(factory.StaffId);

        // 1. Status filter
        var statusRes = await Json(await staff.GetAsync($"/api/v2/staff/orders?status=printing&pageSize=100"));
        var statusItems = statusRes.GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(statusItems, x => x.GetProperty("id").GetGuid() == order2Id);
        Assert.DoesNotContain(statusItems, x => x.GetProperty("id").GetGuid() == order1Id);

        // 2. OrderCode exact match with trim
        var codeRes = await Json(await staff.GetAsync($"/api/v2/staff/orders?orderCode=%20%20{code2}%20%20&pageSize=100"));
        var codeItems = codeRes.GetProperty("items").EnumerateArray().ToList();
        Assert.Single(codeItems);
        Assert.Equal(order2Id, codeItems[0].GetProperty("id").GetGuid());

        // 3. Age filter
        var ageRes = await Json(await staff.GetAsync($"/api/v2/staff/orders?minAgeMinutes=15&pageSize=100"));
        var ageItems = ageRes.GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(ageItems, x => x.GetProperty("id").GetGuid() == order1Id);
        Assert.Contains(ageItems, x => x.GetProperty("id").GetGuid() == order2Id);
        Assert.DoesNotContain(ageItems, x => x.GetProperty("id").GetGuid() == order3Id);

        // 4. Milestone preserved in DTO
        var item2 = statusItems.Single(x => x.GetProperty("id").GetGuid() == order2Id);
        Assert.True(item2.TryGetProperty("printingStartedAtUtc", out var printingStarted));
        Assert.NotNull(printingStarted.GetString());
    }

    [Fact]
    public async Task AC04_PagingAndSort_DeterministicTieBreaker_PageBeyondData()
    {
        var testNow = new DateTime(2026, 10, 25, 10, 0, 0, DateTimeKind.Utc);
        var idA = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var idB = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var idC = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var tag = "Sort_" + Guid.NewGuid().ToString("N")[..8];

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.GenerationTasks.AddRange(
                new GenerationTask
                {
                    Id = idB,
                    UserId = factory.CustomerId,
                    ModelName = $"SortTest_{tag}_B",
                    Provider = tag,
                    CreatedAtUtc = testNow
                },
                new GenerationTask
                {
                    Id = idA,
                    UserId = factory.CustomerId,
                    ModelName = $"SortTest_{tag}_A",
                    Provider = tag,
                    CreatedAtUtc = testNow
                },
                new GenerationTask
                {
                    Id = idC,
                    UserId = factory.CustomerId,
                    ModelName = $"SortTest_{tag}_C",
                    Provider = tag,
                    CreatedAtUtc = testNow.AddMinutes(5)
                }
            );
            await db.SaveChangesAsync();
        }

        using var staff = await factory.Client(factory.StaffId);

        // Sort oldest: testNow first (idA then idB by tie-breaker ASC), then testNow+5m (idC)
        var oldestRes = await Json(await staff.GetAsync($"/api/v2/staff/generation-jobs?provider={tag}&sort=oldest"));
        var oldestItems = oldestRes.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToList();
        Assert.Equal(new[] { idA, idB, idC }, oldestItems);

        // Sort newest: testNow+5m first (idC), then testNow (idB then idA by tie-breaker DESC)
        var newestRes = await Json(await staff.GetAsync($"/api/v2/staff/generation-jobs?provider={tag}&sort=newest"));
        var newestItems = newestRes.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToList();
        Assert.Equal(new[] { idC, idB, idA }, newestItems);

        // Paging: page 1 size 2 -> items length 2, totalCount = 3
        var page1 = await Json(await staff.GetAsync($"/api/v2/staff/generation-jobs?provider={tag}&page=1&pageSize=2"));
        Assert.Equal(2, page1.GetProperty("items").GetArrayLength());
        Assert.Equal(1, page1.GetProperty("page").GetInt32());
        Assert.Equal(2, page1.GetProperty("pageSize").GetInt32());
        Assert.Equal(3, page1.GetProperty("totalCount").GetInt32());

        // Paging: page 2 size 2 -> items length 1, totalCount = 3
        var page2 = await Json(await staff.GetAsync($"/api/v2/staff/generation-jobs?provider={tag}&page=2&pageSize=2"));
        Assert.Equal(1, page2.GetProperty("items").GetArrayLength());
        Assert.Equal(3, page2.GetProperty("totalCount").GetInt32());

        // Page beyond data: page 3 size 2 -> items length 0, totalCount = 3
        var page3 = await Json(await staff.GetAsync($"/api/v2/staff/generation-jobs?provider={tag}&page=3&pageSize=2"));
        Assert.Equal(0, page3.GetProperty("items").GetArrayLength());
        Assert.Equal(3, page3.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task AC05_SafeDto_DoesNotLeakRawErrors_Paths_ProviderIds_Fingerprint_Json_PII_Budgets()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var secretId = Guid.NewGuid();
        var secretOrderId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.GenerationTasks.Add(new GenerationTask
            {
                Id = secretId,
                UserId = factory.CustomerId,
                ModelName = $"SafeTask_{tag}",
                Provider = "SafeMeshy",
                ProviderTaskId = "SECRET_PROVIDER_TASK_ID_999",
                ResizeTaskId = "SECRET_RESIZE_TASK_ID_888",
                ConvertTaskId = "SECRET_CONVERT_TASK_ID_777",
                AnalysisTaskId = "SECRET_ANALYSIS_TASK_ID_666",
                ResizedModelUrl = "https://secrets.mesh.ai/resized.glb",
                StoragePathGlb = "C:\\secrets\\models\\secret.glb",
                StoragePathStl = "C:\\secrets\\models\\secret.stl",
                InputImageUrl = "C:\\secrets\\models\\input.png",
                CleanedImageUrl = "C:\\secrets\\models\\cleaned.png",
                ThumbnailUrl = "https://secrets.mesh.ai/thumb.png",
                RequestFingerprint = "SECRET_FINGERPRINT_12345",
                ClientRequestId = "SECRET_CLIENT_REQ_67890",
                ErrorMessage = "SECRET_RAW_ERROR_MESSAGE_FAILED_IN_PROVIDER",
                PrintabilityJson = "{\"secret_internal_detail\":true}",
                ReviewNote = "Internal review note"
            });

            db.Orders.Add(new Order
            {
                Id = secretOrderId,
                OrderCode = $"TB-ORD-SAFE-{tag}",
                UserId = factory.CustomerId,
                GenerationTaskId = secretId,
                PricingPackageId = DatabaseInitializer.DemoPackageId,
                Status = OrderStatus.Draft,
                UnitPriceSnapshot = 999999m,
                PartnerCostSnapshot = 888888m,
                AiCostBudgetSnapshot = 777777m,
                DefectBudgetSnapshot = 666666m,
                ShippingSubsidySnapshot = 555555m,
                PaymentGatewayFeeSnapshot = 444444m,
                SubTotalVnd = 999999m,
                ShippingFeeRaw = 333333m,
                FinalShippingFee = 222222m,
                TotalAmount = 1222221m,
                ReceiverName = "SECRET_RECEIVER_JOHN_DOE",
                ReceiverPhone = "0987654321_SECRET",
                StreetAddress = "SECRET_STREET_123",
                District = "SECRET_DISTRICT_456",
                City = "SECRET_CITY_789",
                CustomerNote = "SECRET_CUSTOMER_SPECIAL_NOTE"
            });

            await db.SaveChangesAsync();
        }

        using var staff = await factory.Client(factory.StaffId);

        // 1. Generation jobs response inspection
        var jobResponse = await staff.GetAsync($"/api/v2/staff/generation-jobs?provider=SafeMeshy");
        var jobRawJson = await jobResponse.Content.ReadAsStringAsync();

        Assert.Contains(secretId.ToString(), jobRawJson);
        Assert.DoesNotContain("SECRET_PROVIDER_TASK_ID_999", jobRawJson);
        Assert.DoesNotContain("SECRET_RESIZE_TASK_ID_888", jobRawJson);
        Assert.DoesNotContain("SECRET_CONVERT_TASK_ID_777", jobRawJson);
        Assert.DoesNotContain("SECRET_ANALYSIS_TASK_ID_666", jobRawJson);
        Assert.DoesNotContain("secrets.mesh.ai", jobRawJson);
        Assert.DoesNotContain("C:\\secrets", jobRawJson);
        Assert.DoesNotContain("SECRET_FINGERPRINT_12345", jobRawJson);
        Assert.DoesNotContain("SECRET_CLIENT_REQ_67890", jobRawJson);
        Assert.DoesNotContain("SECRET_RAW_ERROR_MESSAGE_FAILED_IN_PROVIDER", jobRawJson);
        Assert.DoesNotContain("secret_internal_detail", jobRawJson);
        Assert.DoesNotContain("errorMessage", jobRawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("storagePath", jobRawJson, StringComparison.OrdinalIgnoreCase);

        // 2. Orders response inspection
        var orderResponse = await staff.GetAsync($"/api/v2/staff/orders?orderCode=TB-ORD-SAFE-{tag}");
        var orderRawJson = await orderResponse.Content.ReadAsStringAsync();

        Assert.Contains(secretOrderId.ToString(), orderRawJson);
        Assert.DoesNotContain("SECRET_RECEIVER_JOHN_DOE", orderRawJson);
        Assert.DoesNotContain("0987654321_SECRET", orderRawJson);
        Assert.DoesNotContain("SECRET_STREET_123", orderRawJson);
        Assert.DoesNotContain("SECRET_DISTRICT_456", orderRawJson);
        Assert.DoesNotContain("SECRET_CITY_789", orderRawJson);
        Assert.DoesNotContain("SECRET_CUSTOMER_SPECIAL_NOTE", orderRawJson);
        Assert.DoesNotContain("888888", orderRawJson);
        Assert.DoesNotContain("777777", orderRawJson);
        Assert.DoesNotContain("666666", orderRawJson);
        Assert.DoesNotContain("receiverName", orderRawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("receiverPhone", orderRawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("streetAddress", orderRawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("customerNote", orderRawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("totalAmount", orderRawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("unitPriceSnapshot", orderRawJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AC06_Validation_InvalidParams_Returns400ProblemDetailsWithTraceId()
    {
        using var staff = await factory.Client(factory.StaffId);

        // Helper to assert ProblemDetails with status 400 and traceId
        static async Task AssertProblemDetails400(HttpResponseMessage response)
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(body).RootElement;
            Assert.Equal(400, doc.GetProperty("status").GetInt32());
            Assert.True(doc.TryGetProperty("traceId", out var traceIdProp));
            Assert.False(string.IsNullOrWhiteSpace(traceIdProp.GetString()));
            Assert.True(doc.TryGetProperty("detail", out var detailProp));
            Assert.False(string.IsNullOrWhiteSpace(detailProp.GetString()));
        }

        // Invalid enum string
        await AssertProblemDetails400(await staff.GetAsync("/api/v2/staff/generation-jobs?status=invalid_enum"));
        await AssertProblemDetails400(await staff.GetAsync("/api/v2/staff/orders?status=invalid_enum"));

        // Undefined enum number
        await AssertProblemDetails400(await staff.GetAsync("/api/v2/staff/generation-jobs?status=999"));
        await AssertProblemDetails400(await staff.GetAsync("/api/v2/staff/generation-jobs?status=-1"));
        await AssertProblemDetails400(await staff.GetAsync("/api/v2/staff/generation-jobs?reviewStatus=999"));
        await AssertProblemDetails400(await staff.GetAsync("/api/v2/staff/orders?status=999"));

        // Sort not in allowlist
        await AssertProblemDetails400(await staff.GetAsync("/api/v2/staff/generation-jobs?sort=random"));
        await AssertProblemDetails400(await staff.GetAsync("/api/v2/staff/orders?sort=createdAtUtc"));

        // Page/size invalid and overflow
        await AssertProblemDetails400(await staff.GetAsync("/api/v2/staff/generation-jobs?page=0"));
        await AssertProblemDetails400(await staff.GetAsync("/api/v2/staff/generation-jobs?page=-1"));
        await AssertProblemDetails400(await staff.GetAsync("/api/v2/staff/generation-jobs?page=100001"));
        await AssertProblemDetails400(await staff.GetAsync("/api/v2/staff/generation-jobs?page=2147483647"));
        await AssertProblemDetails400(await staff.GetAsync("/api/v2/staff/generation-jobs?page=9999999999999"));
        await AssertProblemDetails400(await staff.GetAsync("/api/v2/staff/generation-jobs?page=abc"));
        await AssertProblemDetails400(await staff.GetAsync("/api/v2/staff/generation-jobs?pageSize=0"));
        await AssertProblemDetails400(await staff.GetAsync("/api/v2/staff/generation-jobs?pageSize=101"));

        // Provider / OrderCode too long
        var longProvider = new string('x', 31);
        await AssertProblemDetails400(await staff.GetAsync($"/api/v2/staff/generation-jobs?provider={longProvider}"));
        var longOrderCode = new string('x', 51);
        await AssertProblemDetails400(await staff.GetAsync($"/api/v2/staff/orders?orderCode={longOrderCode}"));

        // Negative minAgeMinutes
        await AssertProblemDetails400(await staff.GetAsync("/api/v2/staff/generation-jobs?minAgeMinutes=-1"));
        await AssertProblemDetails400(await staff.GetAsync("/api/v2/staff/orders?minAgeMinutes=-10"));

        // Malformed date
        await AssertProblemDetails400(await staff.GetAsync("/api/v2/staff/generation-jobs?createdFromUtc=invalid_date"));

        // Missing-timezone date (no 'Z' and no offset)
        await AssertProblemDetails400(await staff.GetAsync("/api/v2/staff/generation-jobs?createdFromUtc=2026-10-25T10:00:00"));
        await AssertProblemDetails400(await staff.GetAsync("/api/v2/staff/generation-jobs?createdToUtc=2026-10-25"));

        // from >= to
        await AssertProblemDetails400(await staff.GetAsync(
            "/api/v2/staff/generation-jobs?createdFromUtc=2026-10-25T12:00:00Z&createdToUtc=2026-10-25T10:00:00Z"));
        await AssertProblemDetails400(await staff.GetAsync(
            "/api/v2/staff/generation-jobs?createdFromUtc=2026-10-25T10:00:00Z&createdToUtc=2026-10-25T10:00:00Z"));
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"HTTP {(int)response.StatusCode}: {body}");
        return JsonSerializer.Deserialize<JsonElement>(body);
    }
}
