using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheBoys.Application.Quotes;
using TheBoys.Application.Reviews;
using TheBoys.Domain.Entities;
using TheBoys.Infrastructure.Persistence;
using TheBoys.WebApi;

namespace TheBoys.Tests.Integration;

public class OrderV2ApiFlowTests(DemoFactory factory) : IClassFixture<DemoFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    [Fact]
    public async Task CompleteV2OrderFlow_FromReview_ToApproval_ToQuote_ToAtomicCheckout()
    {
        using var customer = await factory.Client(factory.CustomerId);
        using var other = await factory.Client(factory.OtherId);
        using var staff = await factory.Client(factory.StaffId);

        // 1. Setup a valid published ModelVersion with print asset for Customer
        Guid versionId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var project = new ModelProject
            {
                Id = Guid.NewGuid(),
                UserId = factory.CustomerId,
                Name = "V2 Order Flow Test Model",
                CreatedAtUtc = DateTime.UtcNow
            };

            var version = new ModelVersion
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                VersionNo = 1,
                GeometryHash = SHA256.HashData(new byte[] { 10, 20, 30 }),
                WidthMm = 60,
                DepthMm = 60,
                HeightMm = 100,
                VolumeCm3 = 30,
                IsWatertight = true,
                PrintabilityStatus = "healthy",
                IsPublished = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            var asset = new ModelAsset
            {
                Id = Guid.NewGuid(),
                VersionId = version.Id,
                Format = "STL",
                Purpose = "Print",
                StorageKey = "models/v2_print.stl",
                Sha256 = SHA256.HashData(new byte[] { 40, 50, 60 }),
                ByteLength = 2048,
                CreatedAtUtc = DateTime.UtcNow
            };
            version.Assets.Add(asset);

            db.ModelProjects.Add(project);
            db.ModelVersions.Add(version);
            await db.SaveChangesAsync();
            versionId = version.Id;
        }

        var config = new
        {
            material = "Resin 16K",
            heightMm = 100m,
            packageCode = "TIER_STANDARD_390"
        };

        var checklist = new PrintReviewChecklistDto
        {
            Orientation = "45 deg",
            SupportConfirmation = true,
            Material = "Resin 16K",
            PrintHeightMm = 100,
            BuildVolumeConfirmation = true
        };

        // 2. Customer cannot review (Forbidden)
        var custReviewRes = await customer.PostAsJsonAsync("/api/v2/reviews", new
        {
            versionId,
            decision = "Approved",
            checklist,
            configuration = config,
            note = "Self approve attempt"
        });
        Assert.Equal(HttpStatusCode.Forbidden, custReviewRes.StatusCode);

        // 3. Staff reviews: first reject, then approve
        var staffRejectRes = await staff.PostAsJsonAsync("/api/v2/reviews", new
        {
            versionId,
            decision = "Rejected",
            checklist,
            configuration = config,
            note = "Độ dày thành chưa đạt"
        });
        Assert.Equal(HttpStatusCode.OK, staffRejectRes.StatusCode);

        var staffApproveRes = await staff.PostAsJsonAsync("/api/v2/reviews", new
        {
            versionId,
            decision = "Approved",
            checklist,
            configuration = config,
            note = "Đã đạt kiểm tra mesh và độ dày"
        });
        Assert.Equal(HttpStatusCode.OK, staffApproveRes.StatusCode);

        // 4. Other user cannot approve (Unauthorized/NotOwner)
        var otherApproveRes = await other.PostAsJsonAsync("/api/v2/approvals", new
        {
            versionId,
            configuration = config,
            policyVersion = "v1.0"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, otherApproveRes.StatusCode);

        // 5. Customer approves own model version
        var custApproveRes = await customer.PostAsJsonAsync("/api/v2/approvals", new
        {
            versionId,
            configuration = config,
            policyVersion = "v1.0"
        });
        Assert.Equal(HttpStatusCode.OK, custApproveRes.StatusCode);

        // 6. Cannot quote with mismatched configuration
        var mismatchedConfig = new
        {
            material = "PLA", // different material -> different hash
            heightMm = 100m,
            packageCode = "TIER_STANDARD_390"
        };

        var address = new ShippingAddressDto
        {
            ReceiverName = "Nguyen Van A",
            ReceiverPhone = "0901234567",
            StreetAddress = "123 Nguyen Hue",
            District = "District 1",
            City = "TP HCM"
        };

        var mismatchedQuoteRes = await customer.PostAsJsonAsync("/api/v2/quotes", new
        {
            versionId,
            configuration = mismatchedConfig,
            pricingPackageId = DatabaseInitializer.DemoPackageId,
            quantity = 2,
            address
        });
        Assert.Equal(HttpStatusCode.Conflict, mismatchedQuoteRes.StatusCode);

        // 7. Customer creates quote with matching configuration
        var validQuoteRes = await customer.PostAsJsonAsync("/api/v2/quotes", new
        {
            versionId,
            configuration = config,
            pricingPackageId = DatabaseInitializer.DemoPackageId,
            quantity = 2,
            address
        });
        Assert.Equal(HttpStatusCode.Created, validQuoteRes.StatusCode);

        var quoteJson = await validQuoteRes.Content.ReadFromJsonAsync<JsonElement>();
        var quoteId = quoteJson.GetProperty("id").GetGuid();
        Assert.Equal(562540m, quoteJson.GetProperty("totalVnd").GetDecimal());

        // 8. Other user cannot read customer's quote
        var otherReadRes = await other.GetAsync($"/api/v2/quotes/{quoteId}");
        Assert.Equal(HttpStatusCode.Unauthorized, otherReadRes.StatusCode);

        // 9. Customer reads own quote
        var custReadRes = await customer.GetAsync($"/api/v2/quotes/{quoteId}");
        Assert.Equal(HttpStatusCode.OK, custReadRes.StatusCode);

        // 10. Checkout without Idempotency-Key header returns BadRequest
        var noKeyRes = await customer.PostAsJsonAsync("/api/v2/orders", new { quoteId });
        Assert.Equal(HttpStatusCode.BadRequest, noKeyRes.StatusCode);

        // 11. Other user cannot checkout customer's quote
        var otherKey = Guid.NewGuid().ToString();
        var otherCheckoutReq = new HttpRequestMessage(HttpMethod.Post, "/api/v2/orders")
        {
            Content = JsonContent.Create(new { quoteId })
        };
        otherCheckoutReq.Headers.Add("Idempotency-Key", otherKey);
        var otherCheckoutRes = await other.SendAsync(otherCheckoutReq);
        Assert.Equal(HttpStatusCode.Unauthorized, otherCheckoutRes.StatusCode);

        // 12. Customer successfully checkouts quote atomically
        var orderKey = Guid.NewGuid().ToString();
        var checkoutReq = new HttpRequestMessage(HttpMethod.Post, "/api/v2/orders")
        {
            Content = JsonContent.Create(new { quoteId })
        };
        checkoutReq.Headers.Add("Idempotency-Key", orderKey);
        var checkoutRes = await customer.SendAsync(checkoutReq);
        Assert.Equal(HttpStatusCode.Created, checkoutRes.StatusCode);

        var orderJson = await checkoutRes.Content.ReadFromJsonAsync<JsonElement>();
        var orderId = orderJson.GetProperty("id").GetGuid();
        Assert.Equal("PendingPayment", orderJson.GetProperty("fulfillmentStatus").GetString());
        Assert.Equal("Unpaid", orderJson.GetProperty("paymentStatus").GetString());
        Assert.Equal(562540m, orderJson.GetProperty("totalVnd").GetDecimal());
        Assert.Equal(quoteId, orderJson.GetProperty("quoteId").GetGuid());

        // Verify items and statusHistories in response
        Assert.Equal(1, orderJson.GetProperty("items").GetArrayLength());
        Assert.Equal(2, orderJson.GetProperty("statusHistories").GetArrayLength());

        // 13. Idempotency replay: same key returns OK with same Order
        var replayReq = new HttpRequestMessage(HttpMethod.Post, "/api/v2/orders")
        {
            Content = JsonContent.Create(new { quoteId })
        };
        replayReq.Headers.Add("Idempotency-Key", orderKey);
        var replayRes = await customer.SendAsync(replayReq);
        Assert.Equal(HttpStatusCode.OK, replayRes.StatusCode);
        var replayJson = await replayRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(orderId, replayJson.GetProperty("id").GetGuid());

        // 14. Idempotency mismatch: same key with different quoteId returns Conflict
        var mismatchReq = new HttpRequestMessage(HttpMethod.Post, "/api/v2/orders")
        {
            Content = JsonContent.Create(new { quoteId = Guid.NewGuid() })
        };
        mismatchReq.Headers.Add("Idempotency-Key", orderKey);
        var mismatchRes = await customer.SendAsync(mismatchReq);
        Assert.Equal(HttpStatusCode.Conflict, mismatchRes.ErrorOrStatusCode());

        // 15. Cannot checkout already checked out quote with a new key
        var secondKey = Guid.NewGuid().ToString();
        var secondReq = new HttpRequestMessage(HttpMethod.Post, "/api/v2/orders")
        {
            Content = JsonContent.Create(new { quoteId })
        };
        secondReq.Headers.Add("Idempotency-Key", secondKey);
        var secondRes = await customer.SendAsync(secondReq);
        Assert.Equal(HttpStatusCode.Conflict, secondRes.StatusCode);
    }

    [Fact]
    public async Task Invariant11_Concurrency_MultipleCheckouts_OnlyOneSucceeds()
    {
        using var customer = await factory.Client(factory.CustomerId);
        using var staff = await factory.Client(factory.StaffId);

        // Create a model version, review, approval, and quote
        Guid versionId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var project = new ModelProject
            {
                Id = Guid.NewGuid(),
                UserId = factory.CustomerId,
                Name = "Concurrency Test Model",
                CreatedAtUtc = DateTime.UtcNow
            };

            var version = new ModelVersion
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                VersionNo = 1,
                GeometryHash = SHA256.HashData(new byte[] { 7, 8, 9 }),
                WidthMm = 50,
                DepthMm = 50,
                HeightMm = 100,
                VolumeCm3 = 20,
                IsWatertight = true,
                PrintabilityStatus = "healthy",
                IsPublished = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            version.Assets.Add(new ModelAsset
            {
                Id = Guid.NewGuid(),
                VersionId = version.Id,
                Format = "STL",
                Purpose = "Print",
                StorageKey = "models/conc.stl",
                Sha256 = SHA256.HashData(new byte[] { 11, 22, 33 }),
                ByteLength = 1024,
                CreatedAtUtc = DateTime.UtcNow
            });

            db.ModelProjects.Add(project);
            db.ModelVersions.Add(version);
            await db.SaveChangesAsync();
            versionId = version.Id;
        }

        var config = new { material = "Resin 16K", heightMm = 100m, packageCode = "TIER_STANDARD_390" };
        var checklist = new PrintReviewChecklistDto
        {
            Orientation = "upright",
            SupportConfirmation = true,
            Material = "Resin 16K",
            PrintHeightMm = 100,
            BuildVolumeConfirmation = true
        };

        await staff.PostAsJsonAsync("/api/v2/reviews", new
        {
            versionId,
            decision = "Approved",
            checklist,
            configuration = config,
            note = "Approved"
        });

        await customer.PostAsJsonAsync("/api/v2/approvals", new
        {
            versionId,
            configuration = config,
            policyVersion = "v1.0"
        });

        var quoteRes = await customer.PostAsJsonAsync("/api/v2/quotes", new
        {
            versionId,
            configuration = config,
            pricingPackageId = DatabaseInitializer.DemoPackageId,
            quantity = 1,
            address = new ShippingAddressDto
            {
                ReceiverName = "Conc User",
                ReceiverPhone = "0900000000",
                StreetAddress = "Conc Street",
                District = "Conc District",
                City = "HCM"
            }
        });
        var quoteJson = await quoteRes.Content.ReadFromJsonAsync<JsonElement>();
        var quoteId = quoteJson.GetProperty("id").GetGuid();

        // Launch 5 concurrent checkout requests with different idempotency keys for the SAME quote
        var tasks = Enumerable.Range(0, 5).Select(async _ =>
        {
            using var client = await factory.Client(factory.CustomerId);
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/v2/orders")
            {
                Content = JsonContent.Create(new { quoteId })
            };
            req.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            return await client.SendAsync(req);
        }).ToList();

        var responses = await Task.WhenAll(tasks);

        var successCount = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        var conflictCount = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);

        // Exactly 1 order created, all others rejected with 409 Conflict
        Assert.Equal(1, successCount);
        Assert.Equal(4, conflictCount);

        // Verify only 1 Order exists in DB for this quoteId
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var orders = await db.Orders.Where(o => o.QuoteId == quoteId).ToListAsync();
            Assert.Single(orders);
        }
    }
}

internal static class HttpResponseExtensions
{
    public static HttpStatusCode ErrorOrStatusCode(this HttpResponseMessage response) => response.StatusCode;
}
