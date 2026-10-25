using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Application.Defects.DTOs;
using TheBoys.Application.Fulfillment.DTOs;
using TheBoys.Application.Notifications.DTOs;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;
using TheBoys.Infrastructure.Persistence;
using TheBoys.WebApi;
using TheBoys.WebApi.Controllers.v2;

namespace TheBoys.Tests.Integration;

public class ReleaseGateRelB09Tests(DemoFactory factory) : IClassFixture<DemoFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    [Fact]
    public async Task GateG1_MigrationAndSchemaIntegrity_AllVNextTablesAccessible()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Kiểm tra truy vấn toàn bộ bảng vNext cốt lõi để khẳng định tính toàn vẹn của schema DB
        Assert.NotNull(await db.Users.FirstOrDefaultAsync());
        Assert.NotNull(await db.PricingPackages.FirstOrDefaultAsync());
        Assert.True(await db.Orders.CountAsync() >= 0);
        Assert.True(await db.PaymentAttempts.CountAsync() >= 0);
        Assert.True(await db.Refunds.CountAsync() >= 0);
        Assert.True(await db.OrderCancellationRequests.CountAsync() >= 0);
        Assert.True(await db.PartnerLabs.CountAsync() >= 0);
        Assert.True(await db.PartnerAssignments.CountAsync() >= 0);
        Assert.True(await db.ProductionRuns.CountAsync() >= 0);
        Assert.True(await db.Shipments.CountAsync() >= 0);
        Assert.True(await db.ShipmentEvents.CountAsync() >= 0);
        Assert.True(await db.DefectReports.CountAsync() >= 0);
        Assert.True(await db.DefectEvidence.CountAsync() >= 0);
        Assert.True(await db.OutboxMessages.CountAsync() >= 0);
        Assert.True(await db.Notifications.CountAsync() >= 0);
        Assert.True(await db.PrivacyRequests.CountAsync() >= 0);
        Assert.True(await db.AuditLogs.CountAsync() >= 0);
    }

    [Fact]
    public async Task GateG2_ObservabilityAndHealthGate_SecuredAndNoSecretsExposed()
    {
        using var customer = await factory.Client(factory.CustomerId);
        using var staff = await factory.Client(factory.StaffId);

        // 1. Kiểm tra /health, /health/live, /health/ready là public anonymous
        var healthRes = await customer.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, healthRes.StatusCode);
        var healthJson = await healthRes.Content.ReadAsStringAsync();
        Assert.Contains("\"status\":\"ok\"", healthJson);
        Assert.Contains("\"readiness\":\"ready\"", healthJson);
        Assert.Contains("\"database\":{\"healthy\":true", healthJson);
        Assert.Contains("\"storage\":{\"healthy\":true", healthJson);

        // Khẳng định không lộ bất kỳ secret / API key nào
        Assert.DoesNotContain("msy_", healthJson);
        Assert.DoesNotContain("tsk_", healthJson);
        Assert.DoesNotContain("Password", healthJson);

        var liveRes = await customer.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, liveRes.StatusCode);

        var readyRes = await customer.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, readyRes.StatusCode);

        // 2. Customer gọi Observability -> 403 Forbidden
        var custObsRes = await customer.GetAsync("/api/v2/admin/observability");
        Assert.Equal(HttpStatusCode.Forbidden, custObsRes.StatusCode);

        // 3. Staff (Management) gọi Observability -> 200 OK với đầy đủ số liệu
        var staffObsRes = await staff.GetAsync("/api/v2/admin/observability");
        Assert.Equal(HttpStatusCode.OK, staffObsRes.StatusCode);
        var report = await staffObsRes.Content.ReadFromJsonAsync<ObservabilityReportDto>(JsonOptions);
        Assert.NotNull(report);
        Assert.NotNull(report.Commerce);
        Assert.NotNull(report.Messaging);
        Assert.Contains("PiiAndSecretRedactor Active", report.SecurityFilterStatus);
    }

    [Fact]
    public async Task GateG4_FullCommercialGoldenPath_E2E_OrderPaid_To_NotificationRead()
    {
        // Khởi tạo một đơn hàng đã thanh toán thành công và một xưởng in đối tác
        Order order;
        PartnerLab lab;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();

            lab = new PartnerLab
            {
                Id = Guid.NewGuid(),
                LabName = "Golden Path Lab",
                ContactPerson = "Lab Lead",
                PhoneNumber = "0933445566",
                District = "District 3",
                Address = "123 Nam Ky Khoi Nghia",
                CapacityPerDay = 50,
                IsActive = true,
                CreatedAtUtc = clock.UtcNow
            };
            db.PartnerLabs.Add(lab);

            order = new Order
            {
                Id = Guid.NewGuid(),
                OrderCode = $"TB-G4-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}",
                UserId = factory.CustomerId,
                PricingPackageId = DatabaseInitializer.DemoPackageId,
                Status = OrderStatus.Paid,
                FulfillmentStatus = "AwaitingAssignment",
                OrderPaymentStatus = "Paid",
                Currency = "VND",
                Quantity = 1,
                UnitPriceSnapshot = 390000m,
                PartnerCostSnapshot = 120000m,
                SubTotalVnd = 390000m,
                TotalAmount = 390000m,
                ReceiverName = "Nguyen Van VIP",
                ReceiverPhone = "0988112233",
                StreetAddress = "789 Pasteur",
                District = "District 3",
                City = "Ho Chi Minh",
                CreatedAtUtc = clock.UtcNow
            };
            db.Orders.Add(order);

            var attempt = PaymentAttempt.Create(order.Id, "Sandbox", $"REF-G4-{Guid.NewGuid():N}", 390000m, "VND", clock.UtcNow.AddMinutes(30), clock.UtcNow);
            attempt.MarkSucceeded($"TX-G4-{Guid.NewGuid():N}", clock.UtcNow);
            db.PaymentAttempts.Add(attempt);

            await db.SaveChangesAsync();
        }

        using var customer = await factory.Client(factory.CustomerId);
        using var staff = await factory.Client(factory.StaffId);

        // 1. Staff phân công đơn cho xưởng -> 201 Created
        var assignReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v2/orders/{order.Id}/assignments")
        {
            Content = JsonContent.Create(new
            {
                partnerLabId = lab.Id,
                partnerCostSnapshotVnd = 120000m,
                decisionNote = "Phân công Golden Path"
            })
        };
        var assignRes = await staff.SendAsync(assignReq);
        Assert.Equal(HttpStatusCode.Created, assignRes.StatusCode);
        var assignment = await assignRes.Content.ReadFromJsonAsync<PartnerAssignmentDto>(JsonOptions);
        Assert.NotNull(assignment);

        // 2. Staff/Partner chấp thuận nhận in -> 200 OK
        var respondReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v2/assignments/{assignment.Id}/respond")
        {
            Content = JsonContent.Create(new { accepted = true, decisionNote = "Xưởng nhận đơn" })
        };
        var respondRes = await staff.SendAsync(respondReq);
        Assert.Equal(HttpStatusCode.OK, respondRes.StatusCode);

        // 3. Khởi tạo lượt in 3D -> 201 Created
        var startRunReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v2/assignments/{assignment.Id}/production-runs")
        {
            Content = JsonContent.Create(new { quantity = 1, actualCostVnd = 120000m })
        };
        var startRunRes = await staff.SendAsync(startRunReq);
        Assert.Equal(HttpStatusCode.Created, startRunRes.StatusCode);
        var run = await startRunRes.Content.ReadFromJsonAsync<ProductionRunDto>(JsonOptions);
        Assert.NotNull(run);

        // 4. Nghiệm thu QC Pass kèm bằng chứng ảnh chụp -> 200 OK (Chuyển sang Packaged)
        var qcReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v2/production-runs/{run.Id}/qc")
        {
            Content = JsonContent.Create(new
            {
                passed = true,
                qcEvidenceStorageKey = "s3://qc/golden-pass.jpg",
                qcNote = "Đạt tiêu chuẩn 100% không bọt khí",
                actualCostVnd = 120000m
            })
        };
        var qcRes = await staff.SendAsync(qcReq);
        Assert.Equal(HttpStatusCode.OK, qcRes.StatusCode);

        // 5. Tạo vận đơn giao hàng -> 201 Created
        var trackingNo = $"VNPOST-G4-{Guid.NewGuid():N}";
        var shipmentReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v2/orders/{order.Id}/shipments")
        {
            Content = JsonContent.Create(new
            {
                carrier = "VNPost",
                trackingNumber = trackingNo,
                shippingActualVnd = 25000m
            })
        };
        var shipmentRes = await staff.SendAsync(shipmentReq);
        Assert.Equal(HttpStatusCode.Created, shipmentRes.StatusCode);
        var shipment = await shipmentRes.Content.ReadFromJsonAsync<ShipmentDto>(JsonOptions);
        Assert.NotNull(shipment);

        // 6. Cập nhật sự kiện Delivered kèm bằng chứng giao hàng thành công -> 201 Created
        var deliverEventReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v2/shipments/{shipment.Id}/events")
        {
            Content = JsonContent.Create(new
            {
                eventKey = $"DELIVERED-{Guid.NewGuid():N}",
                status = "Delivered",
                note = "Khách đã ký nhận",
                deliveryEvidenceStorageKey = "s3://proof/customer_signature.jpg"
            })
        };
        var deliverEventRes = await staff.SendAsync(deliverEventReq);
        Assert.Equal(HttpStatusCode.Created, deliverEventRes.StatusCode);

        // 7. Staff kích hoạt quét và phát Outbox Batch -> 200 OK
        var dispatchReq = new HttpRequestMessage(HttpMethod.Post, "/api/v2/admin/outbox/dispatch")
        {
            Content = JsonContent.Create(new { batchSize = 50 })
        };
        var dispatchRes = await staff.SendAsync(dispatchReq);
        Assert.Equal(HttpStatusCode.OK, dispatchRes.StatusCode);
        var batchResult = await dispatchRes.Content.ReadFromJsonAsync<OutboxDispatchBatchResult>(JsonOptions);
        Assert.NotNull(batchResult);
        Assert.True(batchResult.ProcessedCount > 0);

        // 8. Customer truy vấn danh sách thông báo -> Nhận các thông báo về đơn hàng
        var notifs = await customer.GetFromJsonAsync<List<NotificationDto>>("/api/v2/notifications", JsonOptions);
        Assert.NotNull(notifs);
        Assert.NotEmpty(notifs);

        var firstNotif = notifs[0];
        Assert.Equal("InApp", firstNotif.Channel);
        Assert.False(string.IsNullOrWhiteSpace(firstNotif.Title));
        Assert.False(string.IsNullOrWhiteSpace(firstNotif.Body));
        Assert.Null(firstNotif.ReadAtUtc);

        // 9. Customer đánh dấu đã đọc thông báo -> 200 OK
        var markReadRes = await customer.PostAsync($"/api/v2/notifications/{firstNotif.Id}/read", null);
        Assert.Equal(HttpStatusCode.OK, markReadRes.StatusCode);
        var updatedNotif = await markReadRes.Content.ReadFromJsonAsync<NotificationDto>(JsonOptions);
        Assert.NotNull(updatedNotif!.ReadAtUtc);
    }
}
