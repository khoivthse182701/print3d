using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;
using TheBoys.Infrastructure.Payments;
using TheBoys.Infrastructure.Persistence;
using TheBoys.Tests;
using TheBoys.WebApi;

namespace TheBoys.Tests.Integration;

public class PayOSWebhookIntegrationTests(DemoFactory factory) : IClassFixture<DemoFactory>
{
    private const string ChecksumKey = "test-checksum-key-1234567890abcdef";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private async Task<(Order order, PaymentAttempt attempt)> SeedPendingOrderAndAttemptAsync(string merchantRef, decimal amount = 390000m)
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
            Status = OrderStatus.PendingPayment,
            FulfillmentStatus = "PendingPayment",
            OrderPaymentStatus = "Unpaid",
            Currency = "VND",
            Quantity = 1,
            UnitPriceSnapshot = amount,
            SubTotalVnd = amount,
            ShippingFeeRaw = 0,
            FinalShippingFee = 0,
            TotalAmount = amount,
            ReceiverName = "Tran Thi PayOS",
            ReceiverPhone = "0987654321",
            StreetAddress = "456 Nguyen Hue",
            District = "District 1",
            City = "Ho Chi Minh",
            CreatedAtUtc = DateTime.UtcNow
        };

        db.Orders.Add(order);

        var attempt = PaymentAttempt.Create(
            order.Id,
            "PayOS",
            merchantRef,
            amount,
            "VND",
            DateTime.UtcNow.AddMinutes(15),
            DateTime.UtcNow,
            Guid.NewGuid().ToString(),
            "payos-fingerprint"
        );
        db.PaymentAttempts.Add(attempt);

        await db.SaveChangesAsync();
        return (order, attempt);
    }

    [Fact]
    public async Task HandleWebhook_PayOS_ValidSignedPayload_CapturesPayment_UpdatesOrderToPaid()
    {
        var merchantRef = $"TB-PAYOS-{Guid.NewGuid():N}";
        var (order, attempt) = await SeedPendingOrderAndAttemptAsync(merchantRef, 390000m);

        var orderCode = 7770001L;
        var dataObj = new
        {
            orderCode,
            amount = 390000,
            description = merchantRef,
            accountNumber = "1234567890",
            reference = $"TX-PAYOS-{Guid.NewGuid():N}",
            transactionDateTime = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
            currency = "VND",
            paymentLinkId = "plink-001",
            code = "00",
            desc = "success"
        };

        var dataElement = JsonDocument.Parse(JsonSerializer.Serialize(dataObj)).RootElement;
        var signature = PayOSPaymentGateway.ComputeWebhookSignature(dataElement, ChecksumKey);

        var payload = new
        {
            code = "00",
            desc = "success",
            data = dataObj,
            signature
        };

        var rawBody = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
        using var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v2/payments/PayOS/webhook")
        {
            Content = new ByteArrayContent(rawBody)
        };
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var resJson = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.True(resJson.GetProperty("acknowledged").GetBoolean());
        Assert.Equal("Applied", resJson.GetProperty("processingResult").GetString());

        // Verify database side-effects
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var updatedOrder = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == order.Id);
        Assert.Equal("Paid", updatedOrder.OrderPaymentStatus);
        Assert.Equal("AwaitingAssignment", updatedOrder.FulfillmentStatus);
        Assert.Equal(OrderStatus.Paid, updatedOrder.Status);
        Assert.NotNull(updatedOrder.PaidAtUtc);

        var updatedAttempt = await db.PaymentAttempts.AsNoTracking().SingleAsync(a => a.Id == attempt.Id);
        Assert.Equal(PaymentAttemptStatus.Succeeded, updatedAttempt.Status);

        var paymentEvent = await db.PaymentEvents.AsNoTracking().SingleAsync(e => e.Gateway == "PayOS" && e.EventId == orderCode.ToString());
        Assert.Equal(PaymentEventProcessingResult.Applied, paymentEvent.ProcessingResult);
    }

    [Fact]
    public async Task HandleWebhook_PayOS_TestPingFromDashboard_AcknowledgedWith200OK()
    {
        // Standard test payload sent by PayOS when merchant clicks "Xác nhận Webhook" in my.payos.vn
        var dataObj = new
        {
            orderCode = 123L,
            amount = 1000,
            description = "VQRIO123",
            accountNumber = "123456789",
            reference = "123",
            transactionDateTime = "2026-01-01 00:00:00",
            currency = "VND",
            paymentLinkId = "test-link-id",
            code = "00",
            desc = "success"
        };

        var dataElement = JsonDocument.Parse(JsonSerializer.Serialize(dataObj)).RootElement;
        var signature = PayOSPaymentGateway.ComputeWebhookSignature(dataElement, ChecksumKey);

        var payload = new
        {
            code = "00",
            desc = "success",
            data = dataObj,
            signature
        };

        var rawBody = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
        using var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v2/payments/PayOS/webhook")
        {
            Content = new ByteArrayContent(rawBody)
        };
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var resJson = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.True(resJson.GetProperty("acknowledged").GetBoolean());
        Assert.Equal("WebhookTestAcknowledged", resJson.GetProperty("processingResult").GetString());
        Assert.Contains("PayOS test webhook acknowledged.", resJson.GetProperty("message").GetString());
    }

    [Fact]
    public async Task HandleWebhook_PayOS_TamperedSignature_Returns401Unauthorized()
    {
        var payload = new
        {
            code = "00",
            desc = "success",
            data = new
            {
                orderCode = 999999L,
                amount = 390000,
                description = "TB-TAMPERED-TEST"
            },
            signature = "tampered_fake_signature"
        };

        var rawBody = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
        using var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v2/payments/PayOS/webhook")
        {
            Content = new ByteArrayContent(rawBody)
        };
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task HandleWebhook_PayOS_DuplicateWebhook_Returns200OKWithoutDuplicateCapture()
    {
        var merchantRef = $"TB-PAYOS-DUP-{Guid.NewGuid():N}";
        var (order, _) = await SeedPendingOrderAndAttemptAsync(merchantRef, 390000m);

        var orderCode = 8880002L;
        var dataObj = new
        {
            orderCode,
            amount = 390000,
            description = merchantRef,
            currency = "VND",
            code = "00",
            desc = "success"
        };

        var dataElement = JsonDocument.Parse(JsonSerializer.Serialize(dataObj)).RootElement;
        var signature = PayOSPaymentGateway.ComputeWebhookSignature(dataElement, ChecksumKey);

        var payload = new
        {
            code = "00",
            desc = "success",
            data = dataObj,
            signature
        };

        var rawBody = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
        using var client = factory.CreateClient();

        // Lần 1: Applied
        var req1 = new HttpRequestMessage(HttpMethod.Post, "/api/v2/payments/PayOS/webhook") { Content = new ByteArrayContent(rawBody) };
        req1.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        var res1 = await client.SendAsync(req1);
        Assert.Equal(HttpStatusCode.OK, res1.StatusCode);
        var json1 = await res1.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("Applied", json1.GetProperty("processingResult").GetString());

        // Lần 2: Trùng lặp EventId -> ACK duplicate
        var req2 = new HttpRequestMessage(HttpMethod.Post, "/api/v2/payments/PayOS/webhook") { Content = new ByteArrayContent(rawBody) };
        req2.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        var res2 = await client.SendAsync(req2);
        Assert.Equal(HttpStatusCode.OK, res2.StatusCode);
        var json2 = await res2.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.True(json2.GetProperty("acknowledged").GetBoolean());
        Assert.Contains("duplicate", json2.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleWebhook_PayOS_AmountMismatch_MarksNeedsAttention()
    {
        var merchantRef = $"TB-PAYOS-MISMATCH-{Guid.NewGuid():N}";
        var (order, _) = await SeedPendingOrderAndAttemptAsync(merchantRef, 390000m);

        // Khách chuyển 200,000 VND thay vì 390,000 VND
        var orderCode = 9990003L;
        var dataObj = new
        {
            orderCode,
            amount = 200000,
            description = merchantRef,
            currency = "VND",
            code = "00",
            desc = "success"
        };

        var dataElement = JsonDocument.Parse(JsonSerializer.Serialize(dataObj)).RootElement;
        var signature = PayOSPaymentGateway.ComputeWebhookSignature(dataElement, ChecksumKey);

        var payload = new
        {
            code = "00",
            desc = "success",
            data = dataObj,
            signature
        };

        var rawBody = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
        using var client = factory.CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v2/payments/PayOS/webhook") { Content = new ByteArrayContent(rawBody) };
        req.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        var res = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var json = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.True(json.GetProperty("acknowledged").GetBoolean());
        Assert.Equal("NeedsAttention", json.GetProperty("processingResult").GetString());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var updatedOrder = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == order.Id);
        Assert.Equal("NeedsAttention", updatedOrder.OrderPaymentStatus);
    }
}
