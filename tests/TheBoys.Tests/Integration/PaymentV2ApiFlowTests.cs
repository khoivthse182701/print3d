using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheBoys.Application.Payments.DTOs;
using TheBoys.Domain.Entities;
using TheBoys.Infrastructure.Payments;
using TheBoys.Infrastructure.Persistence;
using TheBoys.WebApi;

namespace TheBoys.Tests.Integration;

public class PaymentV2ApiFlowTests(DemoFactory factory) : IClassFixture<DemoFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private async Task<Order> SeedPendingOrderAsync(decimal amount = 390000m)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderCode = $"TB-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}",
            UserId = factory.CustomerId,
            QuoteId = null,
            PricingPackageId = DatabaseInitializer.DemoPackageId,
            Status = Domain.Enums.OrderStatus.PendingPayment,
            FulfillmentStatus = "PendingPayment",
            OrderPaymentStatus = "Unpaid",
            Currency = "VND",
            Quantity = 1,
            UnitPriceSnapshot = amount,
            SubTotalVnd = amount,
            ShippingFeeRaw = 0,
            FinalShippingFee = 0,
            TotalAmount = amount,
            ReceiverName = "Nguyen Van Test",
            ReceiverPhone = "0912345678",
            StreetAddress = "123 Le Loi",
            District = "District 1",
            City = "Ho Chi Minh",
            CreatedAtUtc = DateTime.UtcNow
        };

        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order;
    }

    [Fact]
    public async Task FullEndToEndPaymentFlow_ViaApi_InitiateAttempt_AndVerifySignedWebhook()
    {
        using var customer = await factory.Client(factory.CustomerId);
        using var other = await factory.Client(factory.OtherId);

        var order = await SeedPendingOrderAsync(390000m);

        // 1. Other user không thể tạo attempt cho order của Customer (Forbidden / Unauthorized)
        var otherReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v2/orders/{order.Id}/payment-attempts")
        {
            Content = JsonContent.Create(new { note = "Hacker note" })
        };
        otherReq.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var otherRes = await other.SendAsync(otherReq);
        Assert.Equal(HttpStatusCode.Unauthorized, otherRes.StatusCode);

        // 2. Customer tạo PaymentAttempt hợp lệ
        var key = Guid.NewGuid().ToString();
        var attemptReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v2/orders/{order.Id}/payment-attempts")
        {
            Content = JsonContent.Create(new { note = "First attempt" })
        };
        attemptReq.Headers.Add("Idempotency-Key", key);
        var attemptRes = await customer.SendAsync(attemptReq);
        Assert.Equal(HttpStatusCode.Created, attemptRes.StatusCode);

        var attempt = await attemptRes.Content.ReadFromJsonAsync<PaymentAttemptDto>(JsonOptions);
        Assert.NotNull(attempt);
        Assert.Equal("Pending", attempt.Status);
        Assert.NotNull(attempt.CheckoutUrl);
        Assert.Equal(390000m, attempt.AmountVnd);
        Assert.Equal("VND", attempt.Currency);

        // 3. Replay cùng Idempotency-Key trả về attempt cũ (HTTP 200 OK)
        var replayReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v2/orders/{order.Id}/payment-attempts")
        {
            Content = JsonContent.Create(new { note = "First attempt" })
        };
        replayReq.Headers.Add("Idempotency-Key", key);
        var replayRes = await customer.SendAsync(replayReq);
        Assert.Equal(HttpStatusCode.OK, replayRes.StatusCode);
        var replayedAttempt = await replayRes.Content.ReadFromJsonAsync<PaymentAttemptDto>(JsonOptions);
        Assert.Equal(attempt.Id, replayedAttempt!.Id);

        // 4. Client không thể trực tiếp xác nhận thanh toán qua FE redirect hay client call
        // Kiểm tra order vẫn là Unpaid
        var getOrderBefore = await customer.GetFromJsonAsync<JsonElement>($"/api/v2/orders/{order.Id}", JsonOptions);
        Assert.Equal("Unpaid", getOrderBefore.GetProperty("paymentStatus").GetString());
        Assert.Equal("PendingPayment", getOrderBefore.GetProperty("fulfillmentStatus").GetString());

        // 5. Gateway gửi Webhook có chữ ký hợp lệ
        var eventId = $"EVT-API-{Guid.NewGuid():N}";
        var webhookPayload = new
        {
            eventId = eventId,
            merchantReference = attempt.MerchantReference,
            providerTransactionId = $"TX-{Guid.NewGuid():N}",
            amount = 390000m,
            currency = "VND",
            status = "Succeeded"
        };
        var rawBody = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(webhookPayload, JsonOptions));
        var signature = SandboxPaymentGateway.ComputeHmac(rawBody, SandboxPaymentGateway.DefaultSecret);

        using var anonymousClient = factory.CreateClient();
        var webhookReq = new HttpRequestMessage(HttpMethod.Post, "/api/v2/payments/Sandbox/webhook")
        {
            Content = new ByteArrayContent(rawBody)
        };
        webhookReq.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        webhookReq.Headers.Add(SandboxPaymentGateway.SignatureHeader, signature);
        webhookReq.Headers.Add(SandboxPaymentGateway.TimestampHeader, DateTime.UtcNow.ToString("O"));

        var webhookRes = await anonymousClient.SendAsync(webhookReq);
        Assert.Equal(HttpStatusCode.OK, webhookRes.StatusCode);

        // 6. Sau khi webhook xử lý, Order cập nhật thành Paid & AwaitingAssignment
        var getOrderAfter = await customer.GetFromJsonAsync<JsonElement>($"/api/v2/orders/{order.Id}", JsonOptions);
        Assert.Equal("Paid", getOrderAfter.GetProperty("paymentStatus").GetString());
        Assert.Equal("AwaitingAssignment", getOrderAfter.GetProperty("fulfillmentStatus").GetString());

        // 7. Webhook gửi lại cùng EventId (Duplicate) trả về 200 OK
        var dupWebhookReq = new HttpRequestMessage(HttpMethod.Post, "/api/v2/payments/Sandbox/webhook")
        {
            Content = new ByteArrayContent(rawBody)
        };
        dupWebhookReq.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        dupWebhookReq.Headers.Add(SandboxPaymentGateway.SignatureHeader, signature);
        dupWebhookReq.Headers.Add(SandboxPaymentGateway.TimestampHeader, DateTime.UtcNow.ToString("O"));

        var dupWebhookRes = await anonymousClient.SendAsync(dupWebhookReq);
        Assert.Equal(HttpStatusCode.OK, dupWebhookRes.StatusCode);
    }

    [Fact]
    public async Task Concurrency_MultipleSimultaneousDuplicateWebhooks_OnlyOneCaptureOccurs()
    {
        var order = await SeedPendingOrderAsync(390000m);
        using var customer = await factory.Client(factory.CustomerId);

        // Tạo attempt
        var attemptReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v2/orders/{order.Id}/payment-attempts")
        {
            Content = JsonContent.Create(new { note = "Concurrent test" })
        };
        attemptReq.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var attemptRes = await customer.SendAsync(attemptReq);
        var attempt = await attemptRes.Content.ReadFromJsonAsync<PaymentAttemptDto>(JsonOptions);

        var eventId = $"EVT-CONCURRENT-{Guid.NewGuid():N}";
        var webhookPayload = new
        {
            eventId = eventId,
            merchantReference = attempt!.MerchantReference,
            providerTransactionId = $"TX-CONC-{Guid.NewGuid():N}",
            amount = 390000m,
            currency = "VND",
            status = "Succeeded"
        };
        var rawBody = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(webhookPayload, JsonOptions));
        var signature = SandboxPaymentGateway.ComputeHmac(rawBody, SandboxPaymentGateway.DefaultSecret);

        // Bắn 5 request webhook đồng thời với cùng EventId
        var tasks = Enumerable.Range(0, 5).Select(async _ =>
        {
            using var client = factory.CreateClient();
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/v2/payments/Sandbox/webhook")
            {
                Content = new ByteArrayContent(rawBody)
            };
            req.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            req.Headers.Add(SandboxPaymentGateway.SignatureHeader, signature);
            req.Headers.Add(SandboxPaymentGateway.TimestampHeader, DateTime.UtcNow.ToString("O"));
            return await client.SendAsync(req);
        }).ToArray();

        var responses = await Task.WhenAll(tasks);

        // Tất cả 5 request đều nhận được 200 OK (ACK)
        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));

        // Kiểm tra trong DB: chỉ duy nhất 1 bản ghi lịch sử thanh toán thành công
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var captureHistories = await db.OrderStatusHistories
            .Where(h => h.OrderId == order.Id && h.Axis == "Payment" && h.ToStatus == "Paid")
            .ToListAsync();

        Assert.Single(captureHistories);

        var outboxMessages = await db.OutboxMessages
            .Where(o => o.AggregateId == order.Id && o.EventType == "PaymentConfirmed")
            .ToListAsync();

        Assert.Single(outboxMessages);
    }

    [Fact]
    public async Task EndToEnd_OrderCancellationAndFullRefundFlow_ViaApi()
    {
        var order = await SeedPendingOrderAsync(390000m);
        using var customer = await factory.Client(factory.CustomerId);
        using var staff = await factory.Client(factory.StaffId);

        // 1. Tạo PaymentAttempt
        var attemptReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v2/orders/{order.Id}/payment-attempts")
        {
            Content = JsonContent.Create(new { note = "Order before cancel" })
        };
        attemptReq.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var attemptRes = await customer.SendAsync(attemptReq);
        var attempt = await attemptRes.Content.ReadFromJsonAsync<PaymentAttemptDto>(JsonOptions);

        // 2. Webhook xác nhận thanh toán thành công
        var webhookPayload = new
        {
            eventId = $"EVT-FLOW-{Guid.NewGuid():N}",
            merchantReference = attempt!.MerchantReference,
            providerTransactionId = $"TX-FLOW-{Guid.NewGuid():N}",
            amount = 390000m,
            currency = "VND",
            status = "Succeeded"
        };
        var rawBody = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(webhookPayload, JsonOptions));
        var signature = SandboxPaymentGateway.ComputeHmac(rawBody, SandboxPaymentGateway.DefaultSecret);

        using var anonymousClient = factory.CreateClient();
        var webhookReq = new HttpRequestMessage(HttpMethod.Post, "/api/v2/payments/Sandbox/webhook")
        {
            Content = new ByteArrayContent(rawBody)
        };
        webhookReq.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        webhookReq.Headers.Add(SandboxPaymentGateway.SignatureHeader, signature);
        webhookReq.Headers.Add(SandboxPaymentGateway.TimestampHeader, DateTime.UtcNow.ToString("O"));
        var webhookRes = await anonymousClient.SendAsync(webhookReq);
        Assert.Equal(HttpStatusCode.OK, webhookRes.StatusCode);

        // 3. Customer không thể tự hoàn tiền (403 Forbidden)
        var custRefundReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v2/orders/{order.Id}/refunds")
        {
            Content = JsonContent.Create(new { amountVnd = 390000m, reason = "Customer trying to self-refund" })
        };
        custRefundReq.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var custRefundRes = await customer.SendAsync(custRefundReq);
        Assert.Equal(HttpStatusCode.Forbidden, custRefundRes.StatusCode);

        // 4. Customer gửi yêu cầu hủy đơn đã Paid -> Trả về PendingReview và sinh CancellationRequestId
        var cancelReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v2/orders/{order.Id}/cancel")
        {
            Content = JsonContent.Create(new { reason = "Customer requested cancellation before print" })
        };
        var cancelRes = await customer.SendAsync(cancelReq);
        Assert.Equal(HttpStatusCode.OK, cancelRes.StatusCode);

        var cancelResult = await cancelRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("PendingReview", cancelResult.GetProperty("cancellationStatus").GetString());
        var cancellationRequestId = cancelResult.GetProperty("cancellationRequestId").GetGuid();

        // 5. Customer không thể tự duyệt yêu cầu hủy đơn (403 Forbidden)
        var custReviewReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v2/orders/cancellation-requests/{cancellationRequestId}/review")
        {
            Content = JsonContent.Create(new { approved = true, decisionNote = "Customer trying to self-approve" })
        };
        var custReviewRes = await customer.SendAsync(custReviewReq);
        Assert.Equal(HttpStatusCode.Forbidden, custReviewRes.StatusCode);

        // 6. Staff duyệt yêu cầu hủy đơn qua API review cancellation request
        var staffReviewReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v2/orders/cancellation-requests/{cancellationRequestId}/review")
        {
            Content = JsonContent.Create(new { approved = true, decisionNote = "Staff approved cancellation request" })
        };
        var staffReviewRes = await staff.SendAsync(staffReviewReq);
        Assert.Equal(HttpStatusCode.OK, staffReviewRes.StatusCode);

        var getOrderCancelled = await customer.GetFromJsonAsync<JsonElement>($"/api/v2/orders/{order.Id}", JsonOptions);
        Assert.Equal("Cancelled", getOrderCancelled.GetProperty("fulfillmentStatus").GetString());
        Assert.Equal("RefundPending", getOrderCancelled.GetProperty("paymentStatus").GetString());

        // 7. Staff khởi tạo hoàn tiền toàn phần qua POST /api/v2/orders/{id}/refunds -> Trả về 201 Created
        var refundKey = Guid.NewGuid().ToString();
        var refundReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v2/orders/{order.Id}/refunds")
        {
            Content = JsonContent.Create(new { amountVnd = 390000m, reason = "Full refund after cancellation" })
        };
        refundReq.Headers.Add("Idempotency-Key", refundKey);
        var refundRes = await staff.SendAsync(refundReq);
        Assert.Equal(HttpStatusCode.Created, refundRes.StatusCode);

        var refund = await refundRes.Content.ReadFromJsonAsync<RefundDto>(JsonOptions);
        Assert.NotNull(refund);
        Assert.Equal("Succeeded", refund.Status);
        Assert.Equal(390000m, refund.AmountVnd);

        // 8. Replay cùng Idempotency-Key và cùng thông tin -> Trả về 200 OK với cùng dữ liệu (Idempotency Replay)
        var replayReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v2/orders/{order.Id}/refunds")
        {
            Content = JsonContent.Create(new { amountVnd = 390000m, reason = "Full refund replay" })
        };
        replayReq.Headers.Add("Idempotency-Key", refundKey);
        var replayRes = await staff.SendAsync(replayReq);
        Assert.Equal(HttpStatusCode.OK, replayRes.StatusCode);
        var replayed = await replayRes.Content.ReadFromJsonAsync<RefundDto>(JsonOptions);
        Assert.Equal(refund.Id, replayed!.Id);

        // 9. Replay cùng Idempotency-Key nhưng khác số tiền -> Trả về 409 Conflict (IdempotencyMismatch)
        var mismatchReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v2/orders/{order.Id}/refunds")
        {
            Content = JsonContent.Create(new { amountVnd = 200000m, reason = "Mismatch amount replay" })
        };
        mismatchReq.Headers.Add("Idempotency-Key", refundKey);
        var mismatchRes = await staff.SendAsync(mismatchReq);
        Assert.Equal(HttpStatusCode.Conflict, mismatchRes.StatusCode);

        // 10. Kiểm tra trạng thái đơn hàng đã chuyển sang Refunded
        var getOrderRefunded = await customer.GetFromJsonAsync<JsonElement>($"/api/v2/orders/{order.Id}", JsonOptions);
        Assert.Equal("Refunded", getOrderRefunded.GetProperty("paymentStatus").GetString());
        Assert.Equal("Cancelled", getOrderRefunded.GetProperty("fulfillmentStatus").GetString());

        // 11. GET /api/v2/orders/{id}/refunds trả về danh sách có refund vừa tạo
        var listRes = await customer.GetAsync($"/api/v2/orders/{order.Id}/refunds");
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
        var refundsList = await listRes.Content.ReadFromJsonAsync<List<RefundDto>>(JsonOptions);
        Assert.NotNull(refundsList);
        Assert.Single(refundsList);
        Assert.Equal(refund.Id, refundsList[0].Id);
    }

    [Fact]
    public async Task Concurrency_MultipleSimultaneousRefundRequests_CannotExceedCapturedAmount()
    {
        var order = await SeedPendingOrderAsync(390000m);

        // Setup succeeded payment attempt
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var attempt = PaymentAttempt.Create(order.Id, "Sandbox", $"REF-CONC-{Guid.NewGuid():N}", 390000m, "VND", DateTime.UtcNow.AddMinutes(30), DateTime.UtcNow);
            attempt.MarkSucceeded($"TX-CONC-{Guid.NewGuid():N}", DateTime.UtcNow);
            db.PaymentAttempts.Add(attempt);

            var dbOrder = await db.Orders.FindAsync(order.Id);
            dbOrder!.OrderPaymentStatus = "Paid";
            dbOrder.Status = Domain.Enums.OrderStatus.Paid;
            await db.SaveChangesAsync();
        }

        // Bắn 5 request refund song song có SIMULATE_DELAY (50ms external latency)
        // Mỗi request đòi hoàn đủ 390,000 VND với Idempotency-Key khác nhau
        var tasks = Enumerable.Range(0, 5).Select(async _ =>
        {
            using var client = await factory.Client(factory.StaffId);
            var req = new HttpRequestMessage(HttpMethod.Post, $"/api/v2/orders/{order.Id}/refunds")
            {
                Content = JsonContent.Create(new { amountVnd = 390000m, reason = "SIMULATE_DELAY: Concurrent full refund test" })
            };
            req.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            return await client.SendAsync(req);
        }).ToArray();

        var responses = await Task.WhenAll(tasks);

        var successCount = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        var conflictCount = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);

        // Đúng duy nhất 1 request thành công ở bước Reserve, 4 request còn lại bị chặn ngay ở Reserve (409 Conflict)
        Assert.Equal(1, successCount);
        Assert.Equal(4, conflictCount);

        // Kiểm tra trong DB: chỉ có đúng 1 Refund thành công với tổng số tiền 390,000 VND
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var succeededRefunds = await db.Refunds
                .Where(r => r.PaymentAttempt.OrderId == order.Id && r.Status == Domain.Enums.RefundStatus.Succeeded)
                .ToListAsync();

            Assert.Single(succeededRefunds);
            Assert.Equal(390000m, succeededRefunds[0].AmountVnd);
        }
    }
}

