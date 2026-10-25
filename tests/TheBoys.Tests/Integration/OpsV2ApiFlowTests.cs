using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TheBoys.Application.Defects.DTOs;
using TheBoys.Application.Fulfillment.DTOs;
using TheBoys.Application.Payments.DTOs;
using TheBoys.Domain.Entities;
using TheBoys.Infrastructure.Persistence;
using TheBoys.WebApi;

namespace TheBoys.Tests.Integration;

public class OpsV2ApiFlowTests(DemoFactory factory) : IClassFixture<DemoFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private async Task<(Order order, PartnerLab lab)> SeedPaidOrderAndLabAsync(decimal amount = 390000m)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var lab = new PartnerLab
        {
            Id = Guid.NewGuid(),
            LabName = "Saigon 3D Print Lab",
            ContactPerson = "Tran Van Lab",
            PhoneNumber = "0988776655",
            District = "Binh Thanh",
            Address = "12 D2 Street",
            CapacityPerDay = 40,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.PartnerLabs.Add(lab);

        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderCode = $"TB-E2E-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}",
            UserId = factory.CustomerId,
            QuoteId = null,
            PricingPackageId = DatabaseInitializer.DemoPackageId,
            Status = Domain.Enums.OrderStatus.Paid,
            FulfillmentStatus = "AwaitingAssignment",
            OrderPaymentStatus = "Paid",
            Currency = "VND",
            Quantity = 1,
            UnitPriceSnapshot = amount,
            PartnerCostSnapshot = 120000m,
            SubTotalVnd = amount,
            ShippingFeeRaw = 0,
            FinalShippingFee = 0,
            TotalAmount = amount,
            ReceiverName = "Nguyen Van Customer",
            ReceiverPhone = "0912345678",
            StreetAddress = "789 Dien Bien Phu",
            District = "Binh Thanh",
            City = "Ho Chi Minh",
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Orders.Add(order);

        var attempt = PaymentAttempt.Create(order.Id, "Sandbox", $"REF-E2E-{Guid.NewGuid():N}", amount, "VND", DateTime.UtcNow.AddMinutes(30), DateTime.UtcNow);
        attempt.MarkSucceeded($"TX-E2E-{Guid.NewGuid():N}", DateTime.UtcNow);
        db.PaymentAttempts.Add(attempt);

        await db.SaveChangesAsync();
        return (order, lab);
    }

    [Fact]
    public async Task EndToEnd_FulfillmentAndProductionRunFlow_ViaApi()
    {
        var (order, lab) = await SeedPaidOrderAndLabAsync(390000m);

        using var customer = await factory.Client(factory.CustomerId);
        using var staff = await factory.Client(factory.StaffId);

        // 1. Customer không thể tự giao xưởng (403 Forbidden)
        var custAssignReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v2/orders/{order.Id}/assignments")
        {
            Content = JsonContent.Create(new { partnerLabId = lab.Id })
        };
        var custAssignRes = await customer.SendAsync(custAssignReq);
        Assert.Equal(HttpStatusCode.Forbidden, custAssignRes.StatusCode);

        // 2. Staff giao xưởng thành công -> 201 Created
        var staffAssignReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v2/orders/{order.Id}/assignments")
        {
            Content = JsonContent.Create(new
            {
                partnerLabId = lab.Id,
                partnerCostSnapshotVnd = 120000m,
                decisionNote = "Giao xưởng Sài Gòn in sớm"
            })
        };
        var staffAssignRes = await staff.SendAsync(staffAssignReq);
        Assert.Equal(HttpStatusCode.Created, staffAssignRes.StatusCode);
        var assignment = await staffAssignRes.Content.ReadFromJsonAsync<PartnerAssignmentDto>(JsonOptions);
        Assert.NotNull(assignment);
        Assert.Equal("Offered", assignment.Status);

        // 3a. Customer không có quyền phản hồi phân công xưởng -> 403 Forbidden
        var custRespondReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v2/assignments/{assignment.Id}/respond")
        {
            Content = JsonContent.Create(new { accepted = true, decisionNote = "Khách hàng mạo danh xưởng" })
        };
        var custRespondRes = await customer.SendAsync(custRespondReq);
        Assert.Equal(HttpStatusCode.Forbidden, custRespondRes.StatusCode);

        // 3b. Staff (Management) Accept phân công -> 200 OK
        var respondReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v2/assignments/{assignment.Id}/respond")
        {
            Content = JsonContent.Create(new { accepted = true, decisionNote = "Đã nhận đơn và kiểm tra file 3D" })
        };
        var respondRes = await staff.SendAsync(respondReq);
        Assert.Equal(HttpStatusCode.OK, respondRes.StatusCode);

        // 4. Staff bắt đầu lượt in (ProductionRun) -> 201 Created (RunNo = 1, Status = Printing)
        var startRunReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v2/assignments/{assignment.Id}/production-runs")
        {
            Content = JsonContent.Create(new { quantity = 1, actualCostVnd = 120000m })
        };
        var startRunRes = await staff.SendAsync(startRunReq);
        Assert.Equal(HttpStatusCode.Created, startRunRes.StatusCode);
        var run = await startRunRes.Content.ReadFromJsonAsync<ProductionRunDto>(JsonOptions);
        Assert.NotNull(run);
        Assert.Equal(1, run.RunNo);
        Assert.Equal("Printing", run.Status);

        // 4b. Chặn tạo chồng chéo: Không thể khởi tạo lượt in thứ 2 khi lượt in hiện tại đang chạy -> 409 Conflict
        var dupRunReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v2/assignments/{assignment.Id}/production-runs")
        {
            Content = JsonContent.Create(new { quantity = 1 })
        };
        var dupRunRes = await staff.SendAsync(dupRunReq);
        Assert.Equal(HttpStatusCode.Conflict, dupRunRes.StatusCode);

        // 5. Cập nhật tiến độ: PostProcessing -> QualityCheck
        var postStatusReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v2/production-runs/{run.Id}/status")
        {
            Content = JsonContent.Create(new { status = "PostProcessing" })
        };
        var postRes = await staff.SendAsync(postStatusReq);
        Assert.Equal(HttpStatusCode.OK, postRes.StatusCode);

        var qcStatusReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v2/production-runs/{run.Id}/status")
        {
            Content = JsonContent.Create(new { status = "QualityCheck" })
        };
        var qcStatusRes = await staff.SendAsync(qcStatusReq);
        Assert.Equal(HttpStatusCode.OK, qcStatusRes.StatusCode);

        // 6. Ghi nhận QC Đạt -> Chuyển sang Packaged
        var recordQcReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v2/production-runs/{run.Id}/qc")
        {
            Content = JsonContent.Create(new
            {
                passed = true,
                qcEvidenceStorageKey = "s3://qc/e2e-run-pass.jpg",
                qcNote = "Bề mặt láng mịn, dung sai < 0.2mm"
            })
        };
        var recordQcRes = await staff.SendAsync(recordQcReq);
        Assert.Equal(HttpStatusCode.OK, recordQcRes.StatusCode);

        // 7. Tạo vận đơn giao hàng -> 201 Created
        var shipmentReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v2/orders/{order.Id}/shipments")
        {
            Content = JsonContent.Create(new
            {
                carrier = "GHTK",
                trackingNumber = $"GHTK-{Guid.NewGuid():N}",
                shippingActualVnd = 30000m
            })
        };
        var shipmentRes = await staff.SendAsync(shipmentReq);
        Assert.Equal(HttpStatusCode.Created, shipmentRes.StatusCode);
        var shipment = await shipmentRes.Content.ReadFromJsonAsync<ShipmentDto>(JsonOptions);
        Assert.NotNull(shipment);

        // 8. Cập nhật sự kiện vận chuyển: Shipping -> Delivered
        var shipEventReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v2/shipments/{shipment.Id}/events")
        {
            Content = JsonContent.Create(new
            {
                eventKey = $"EVT-SHIP-{Guid.NewGuid():N}",
                status = "Shipping",
                note = "Shipper đang giao hàng"
            })
        };
        var shipEventRes = await staff.SendAsync(shipEventReq);
        Assert.Equal(HttpStatusCode.Created, shipEventRes.StatusCode);

        var deliverEventReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v2/shipments/{shipment.Id}/events")
        {
            Content = JsonContent.Create(new
            {
                eventKey = $"EVT-DELIV-{Guid.NewGuid():N}",
                status = "Delivered",
                note = "Giao hàng thành công",
                deliveryEvidenceStorageKey = "s3://shipment/proof-signature.png"
            })
        };
        var deliverEventRes = await staff.SendAsync(deliverEventReq);
        Assert.Equal(HttpStatusCode.Created, deliverEventRes.StatusCode);

        // 9. Customer xem timeline phân công & giao vận -> 200 OK
        var timelineRes = await customer.GetAsync($"/api/v2/orders/{order.Id}/fulfillment");
        Assert.Equal(HttpStatusCode.OK, timelineRes.StatusCode);
        var timeline = await timelineRes.Content.ReadFromJsonAsync<OrderFulfillmentTimelineDto>(JsonOptions);
        Assert.NotNull(timeline);
        Assert.Equal("Delivered", timeline.FulfillmentStatus);
        Assert.Single(timeline.Assignments);
        Assert.Single(timeline.Shipments);
        Assert.Equal(3, timeline.Shipments[0].Events.Count); // Init + Shipping + Delivered
    }

    [Fact]
    public async Task EndToEnd_DefectReportAndFullRefundResolutionFlow_ViaApi()
    {
        var (order, _) = await SeedPaidOrderAndLabAsync(390000m);

        // Cập nhật order sang Delivered để đủ điều kiện khiếu nại
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var dbOrder = await db.Orders.FindAsync(order.Id);
            dbOrder!.FulfillmentStatus = "Delivered";
            dbOrder.DeliveredAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        using var customer = await factory.Client(factory.CustomerId);
        using var staff = await factory.Client(factory.StaffId);

        // 1. Customer gửi khiếu nại kèm ảnh bằng chứng
        var reqKey = Guid.NewGuid().ToString();
        var defectReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v2/orders/{order.Id}/defect-reports")
        {
            Content = JsonContent.Create(new
            {
                reason = "Sản phẩm bị nứt ở phần đế",
                evidence = new[]
                {
                    new { storageKey = "s3://evidence/crack.jpg", sha256Hex = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855" }
                }
            })
        };
        defectReq.Headers.Add("Idempotency-Key", reqKey);
        var defectRes = await customer.SendAsync(defectReq);
        Assert.Equal(HttpStatusCode.Created, defectRes.StatusCode);

        var report = await defectRes.Content.ReadFromJsonAsync<DefectReportDto>(JsonOptions);
        Assert.NotNull(report);
        Assert.Equal("Open", report.Status);

        // 2. Replay cùng Idempotency-Key -> 200 OK với cùng ReportId
        var replayReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v2/orders/{order.Id}/defect-reports")
        {
            Content = JsonContent.Create(new { reason = "Gửi lại" })
        };
        replayReq.Headers.Add("Idempotency-Key", reqKey);
        var replayRes = await customer.SendAsync(replayReq);
        Assert.Equal(HttpStatusCode.OK, replayRes.StatusCode);
        var replayed = await replayRes.Content.ReadFromJsonAsync<DefectReportDto>(JsonOptions);
        Assert.Equal(report.Id, replayed!.Id);

        // 3. Staff duyệt hoàn tiền (RefundApproved) -> Order chuyển sang RefundPending
        var resolveReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v2/defect-reports/{report.Id}/resolve")
        {
            Content = JsonContent.Create(new
            {
                resolutionType = "RefundApproved",
                resolutionNote = "Đồng ý hoàn tiền 100% do lỗi nứt đế vận chuyển"
            })
        };
        var resolveRes = await staff.SendAsync(resolveReq);
        Assert.Equal(HttpStatusCode.OK, resolveRes.StatusCode);

        var getOrder = await customer.GetFromJsonAsync<JsonElement>($"/api/v2/orders/{order.Id}", JsonOptions);
        Assert.Equal("RefundPending", getOrder.GetProperty("paymentStatus").GetString());

        // 4. Staff gọi API hoàn tiền PAY-B05 để hoàn trả toàn bộ số tiền cho khách
        var refundKey = Guid.NewGuid().ToString();
        var refundReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v2/orders/{order.Id}/refunds")
        {
            Content = JsonContent.Create(new
            {
                amountVnd = 390000m,
                reason = $"Hoàn tiền theo khiếu nại #{report.Id}"
            })
        };
        refundReq.Headers.Add("Idempotency-Key", refundKey);
        var refundRes = await staff.SendAsync(refundReq);
        Assert.Equal(HttpStatusCode.Created, refundRes.StatusCode);

        var refundDto = await refundRes.Content.ReadFromJsonAsync<RefundDto>(JsonOptions);
        Assert.NotNull(refundDto);
        Assert.Equal("Succeeded", refundDto.Status);
        Assert.Equal(390000m, refundDto.AmountVnd);

        // 5. Kiểm tra trạng thái đơn hàng đã là Refunded
        var finalOrder = await customer.GetFromJsonAsync<JsonElement>($"/api/v2/orders/{order.Id}", JsonOptions);
        Assert.Equal("Refunded", finalOrder.GetProperty("paymentStatus").GetString());

        // 6. Kiểm tra DefectReport trên timeline: RefundId == refundDto.Id và Status == "Resolved" (Nối khép kín PAY-B05 ↔ OPS-B07)
        var timeline = await staff.GetFromJsonAsync<OrderFulfillmentTimelineDto>($"/api/v2/orders/{order.Id}/timeline", JsonOptions);
        Assert.NotNull(timeline);
        var resolvedDefect = Assert.Single(timeline.DefectReports);
        Assert.Equal("Resolved", resolvedDefect.Status);
        Assert.Equal(refundDto.Id, resolvedDefect.RefundId);
    }
}
