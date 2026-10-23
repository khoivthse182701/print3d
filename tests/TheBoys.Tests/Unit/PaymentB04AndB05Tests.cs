using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Application.Common.Models;
using TheBoys.Application.Common.Payments;
using TheBoys.Application.Payments.Commands.CreatePaymentAttempt;
using TheBoys.Application.Payments.Commands.ProcessPaymentWebhook;
using TheBoys.Application.Payments.Commands.ReconcilePaymentAttempt;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;
using TheBoys.Infrastructure.Payments;
using TheBoys.Infrastructure.Persistence;
using TheBoys.WebApi;

namespace TheBoys.Tests.Unit;

public class PaymentB04AndB05Tests(DemoFactory factory) : IClassFixture<DemoFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private (IServiceScope scope, ApplicationDbContext db, Order order, IPaymentGatewayRegistry registry, IClock clock) CreateOrderContext(
        string fulfillmentStatus = "PendingPayment",
        string paymentStatus = "Unpaid",
        decimal totalAmount = 390000m)
    {
        var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var registry = scope.ServiceProvider.GetRequiredService<IPaymentGatewayRegistry>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderCode = $"TB-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}",
            UserId = factory.CustomerId,
            QuoteId = null,
            PricingPackageId = DatabaseInitializer.DemoPackageId,
            Status = string.Equals(paymentStatus, "Paid", StringComparison.OrdinalIgnoreCase) ? OrderStatus.Paid :
                     string.Equals(fulfillmentStatus, "Cancelled", StringComparison.OrdinalIgnoreCase) ? OrderStatus.Cancelled : OrderStatus.PendingPayment,
            FulfillmentStatus = fulfillmentStatus,
            OrderPaymentStatus = paymentStatus,
            Currency = "VND",
            Quantity = 1,
            UnitPriceSnapshot = totalAmount,
            SubTotalVnd = totalAmount,
            ShippingFeeRaw = 0,
            FinalShippingFee = 0,
            TotalAmount = totalAmount,
            ReceiverName = "Unit Tester",
            ReceiverPhone = "0901234567",
            StreetAddress = "123 Test Street",
            District = "District 1",
            City = "Ho Chi Minh",
            CreatedAtUtc = DateTime.UtcNow
        };

        db.Orders.Add(order);
        db.SaveChanges();

        return (scope, db, order, registry, clock);
    }

    [Fact]
    public async Task Invariant1_PaymentAttempt_CanOnlyBeCreated_WhenOrderIsPendingPaymentAndUnpaid()
    {
        // 1. Order ở trạng thái PendingPayment & Unpaid -> Tạo thành công
        var (scope1, db1, validOrder, reg1, clock1) = CreateOrderContext("PendingPayment", "Unpaid");
        using var _1 = scope1;
        var handler1 = new CreatePaymentAttemptCommandHandler(db1, reg1, clock1);
        var cmd1 = new CreatePaymentAttemptCommand(factory.CustomerId, validOrder.Id, Guid.NewGuid().ToString("N"), "fingerprint-1");
        var res1 = await handler1.Handle(cmd1, CancellationToken.None);

        Assert.True(res1.IsSuccess);
        Assert.NotNull(res1.Value.Attempt.CheckoutUrl);
        Assert.Equal("Pending", res1.Value.Attempt.Status);

        // 2. Order đã ở trạng thái Paid -> Bị từ chối
        var (scope2, db2, paidOrder, reg2, clock2) = CreateOrderContext("AwaitingAssignment", "Paid");
        using var _2 = scope2;
        var handler2 = new CreatePaymentAttemptCommandHandler(db2, reg2, clock2);
        var cmd2 = new CreatePaymentAttemptCommand(factory.CustomerId, paidOrder.Id, Guid.NewGuid().ToString("N"), "fingerprint-2");
        var res2 = await handler2.Handle(cmd2, CancellationToken.None);

        Assert.True(res2.IsFailure);
        Assert.Equal("InvalidOrderState", res2.Error.Code);
    }

    [Fact]
    public async Task Invariant2_AmountAndCurrency_AlwaysDerivedFromOrderSnapshot_CannotBeAltered()
    {
        var (scope, db, order, reg, clock) = CreateOrderContext("PendingPayment", "Unpaid", totalAmount: 450000m);
        using var _ = scope;
        var handler = new CreatePaymentAttemptCommandHandler(db, reg, clock);
        var cmd = new CreatePaymentAttemptCommand(factory.CustomerId, order.Id, Guid.NewGuid().ToString("N"), "fingerprint-fp");
        var res = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(res.IsSuccess);
        // Số tiền và loại tiền lấy tuyệt đối từ Order, không qua input
        Assert.Equal(450000m, res.Value.Attempt.AmountVnd);
        Assert.Equal("VND", res.Value.Attempt.Currency);
    }

    [Fact]
    public async Task Invariant3_ReplaySameIdempotencyKey_ReturnsSameAttempt()
    {
        var (scope, db, order, reg, clock) = CreateOrderContext();
        using var _ = scope;
        var handler = new CreatePaymentAttemptCommandHandler(db, reg, clock);
        var key = Guid.NewGuid().ToString("N");
        var fingerprint = "same-fingerprint";

        var cmd1 = new CreatePaymentAttemptCommand(factory.CustomerId, order.Id, key, fingerprint);
        var res1 = await handler.Handle(cmd1, CancellationToken.None);
        Assert.True(res1.IsSuccess);
        Assert.False(res1.Value.IsReplay);

        var cmd2 = new CreatePaymentAttemptCommand(factory.CustomerId, order.Id, key, fingerprint);
        var res2 = await handler.Handle(cmd2, CancellationToken.None);
        Assert.True(res2.IsSuccess);
        Assert.True(res2.Value.IsReplay);
        Assert.Equal(res1.Value.Attempt.Id, res2.Value.Attempt.Id);
        Assert.Equal(res1.Value.Attempt.MerchantReference, res2.Value.Attempt.MerchantReference);
    }

    [Fact]
    public async Task Invariant4_SameIdempotencyKey_DifferentFingerprint_Returns409Conflict()
    {
        var (scope, db, order, reg, clock) = CreateOrderContext();
        using var _ = scope;
        var handler = new CreatePaymentAttemptCommandHandler(db, reg, clock);
        var key = Guid.NewGuid().ToString("N");

        var cmd1 = new CreatePaymentAttemptCommand(factory.CustomerId, order.Id, key, "fingerprint-A");
        var res1 = await handler.Handle(cmd1, CancellationToken.None);
        Assert.True(res1.IsSuccess);

        var cmd2 = new CreatePaymentAttemptCommand(factory.CustomerId, order.Id, key, "fingerprint-B");
        var res2 = await handler.Handle(cmd2, CancellationToken.None);
        Assert.True(res2.IsFailure);
        Assert.Equal("IdempotencyMismatch", res2.Error.Code);
    }

    [Fact]
    public async Task Invariant5_UnconfiguredGateway_Returns503_DoesNotCreateFalseSuccess()
    {
        var (scope, db, order, _, clock) = CreateOrderContext();
        using var _ = scope;

        // Giả lập registry có active gateway là SePay chưa cấu hình ApiKey
        var sePayOptions = Options.Create(new PaymentOptions
        {
            ActiveGateway = "SePay",
            SePay = new SePayOptions { ApiKey = "" } // Missing ApiKey
        });
        var sePayGateway = new SePayPaymentGateway(sePayOptions, clock);
        var customRegistry = new PaymentGatewayRegistry(new[] { sePayGateway }, sePayOptions);

        var handler = new CreatePaymentAttemptCommandHandler(db, customRegistry, clock);
        var cmd = new CreatePaymentAttemptCommand(factory.CustomerId, order.Id, Guid.NewGuid().ToString("N"), "fp-sepay");
        var res = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(res.IsFailure);
        Assert.Equal("GatewayNotConfigured", res.Error.Code);
        Assert.Equal(ErrorType.ServiceUnavailable, res.Error.Type);

        // Đảm bảo không có PaymentAttempt nào được đánh dấu Paid hoặc Success
        var attempts = await db.PaymentAttempts.Where(pa => pa.OrderId == order.Id).ToListAsync();
        Assert.Empty(attempts);
    }

    [Fact]
    public async Task Invariant6_GatewayTimeoutOrUnknown_SetsNeedsAttention_DoesNotCreateSecondMerchantReference()
    {
        var (scope, db, order, reg, clock) = CreateOrderContext();
        using var _ = scope;
        var handler = new CreatePaymentAttemptCommandHandler(db, reg, clock);
        var key = Guid.NewGuid().ToString("N");

        // Gửi request với fingerprint mô phỏng timeout
        var cmd = new CreatePaymentAttemptCommand(factory.CustomerId, order.Id, key, "SIMULATE_TIMEOUT");
        var res = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("NeedsAttention", res.Value.Attempt.Status);

        // Khi retry với cùng key: lấy lại đúng attempt đó, không tạo reference thứ hai
        var retryRes = await handler.Handle(cmd, CancellationToken.None);
        Assert.True(retryRes.IsSuccess);
        Assert.True(retryRes.Value.IsReplay);
        Assert.Equal(res.Value.Attempt.MerchantReference, retryRes.Value.Attempt.MerchantReference);
    }

    [Fact]
    public async Task Invariant7_ValidSignedSuccessEvent_TransitionsOrderToPaidAndAwaitingAssignment_AtomicallyOnce()
    {
        var (scope, db, order, reg, clock) = CreateOrderContext(totalAmount: 390000m);
        using var _ = scope;

        // 1. Tạo attempt hợp lệ
        var attemptHandler = new CreatePaymentAttemptCommandHandler(db, reg, clock);
        var attemptRes = await attemptHandler.Handle(new(factory.CustomerId, order.Id, Guid.NewGuid().ToString("N"), "fp"), CancellationToken.None);
        Assert.True(attemptRes.IsSuccess);
        var merchantRef = attemptRes.Value.Attempt.MerchantReference;

        // 2. Chuẩn bị webhook hợp lệ
        var now = clock.UtcNow;
        var eventId = $"EVT-{Guid.NewGuid():N}";
        var webhookPayload = new
        {
            eventId = eventId,
            merchantReference = merchantRef,
            providerTransactionId = $"TX-{Guid.NewGuid():N}",
            amount = 390000m,
            currency = "VND",
            status = "Succeeded"
        };
        var rawBody = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(webhookPayload, JsonOptions));
        var signature = SandboxPaymentGateway.ComputeHmac(rawBody, SandboxPaymentGateway.DefaultSecret);

        var headers = new Dictionary<string, string>
        {
            [SandboxPaymentGateway.SignatureHeader] = signature,
            [SandboxPaymentGateway.TimestampHeader] = now.ToString("O")
        };

        var webhookHandler = new ProcessPaymentWebhookCommandHandler(db, reg, clock);
        var webhookRes = await webhookHandler.Handle(new("Sandbox", rawBody, headers), CancellationToken.None);

        Assert.True(webhookRes.IsSuccess);
        Assert.True(webhookRes.Value.Acknowledged);
        Assert.Equal("Applied", webhookRes.Value.ProcessingResult);

        // 3. Kiểm tra DB: Order cập nhật đúng Paid & AwaitingAssignment
        var updatedOrder = await db.Orders
            .Include(o => o.PaymentAttempts)
            .Include(o => o.StatusHistories)
            .SingleAsync(o => o.Id == order.Id);

        Assert.Equal("Paid", updatedOrder.OrderPaymentStatus);
        Assert.Equal("AwaitingAssignment", updatedOrder.FulfillmentStatus);
        Assert.Equal(OrderStatus.Paid, updatedOrder.Status);
        Assert.NotNull(updatedOrder.PaidAtUtc);

        var updatedAttempt = updatedOrder.PaymentAttempts.Single(pa => pa.MerchantReference == merchantRef);
        Assert.Equal(PaymentAttemptStatus.Succeeded, updatedAttempt.Status);
        Assert.NotNull(updatedAttempt.CapturedAtUtc);

        // Có 2 bản ghi OrderStatusHistory mới (Payment + Fulfillment)
        var paymentHist = updatedOrder.StatusHistories.FirstOrDefault(h => h.Axis == "Payment" && h.ToStatus == "Paid");
        var fulfillHist = updatedOrder.StatusHistories.FirstOrDefault(h => h.Axis == "Fulfillment" && h.ToStatus == "AwaitingAssignment");
        Assert.NotNull(paymentHist);
        Assert.NotNull(fulfillHist);

        // Có OutboxMessage loại PaymentConfirmed
        var outbox = await db.OutboxMessages.FirstOrDefaultAsync(o => o.EventType == "PaymentConfirmed" && o.AggregateId == order.Id);
        Assert.NotNull(outbox);
    }

    [Fact]
    public async Task Invariant8_InvalidSignature_DoesNotMutateOrderOrAttempt()
    {
        var (scope, db, order, reg, clock) = CreateOrderContext(totalAmount: 390000m);
        using var _ = scope;

        var attemptHandler = new CreatePaymentAttemptCommandHandler(db, reg, clock);
        var attemptRes = await attemptHandler.Handle(new(factory.CustomerId, order.Id, Guid.NewGuid().ToString("N"), "fp"), CancellationToken.None);
        var merchantRef = attemptRes.Value.Attempt.MerchantReference;

        var now = clock.UtcNow;
        var webhookPayload = new
        {
            eventId = $"EVT-{Guid.NewGuid():N}",
            merchantReference = merchantRef,
            amount = 390000m,
            currency = "VND",
            status = "Succeeded"
        };
        var rawBody = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(webhookPayload, JsonOptions));

        // Chữ ký giả mạo
        var headers = new Dictionary<string, string>
        {
            [SandboxPaymentGateway.SignatureHeader] = "invalid_forged_signature_12345",
            [SandboxPaymentGateway.TimestampHeader] = now.ToString("O")
        };

        var webhookHandler = new ProcessPaymentWebhookCommandHandler(db, reg, clock);
        var webhookRes = await webhookHandler.Handle(new("Sandbox", rawBody, headers), CancellationToken.None);

        Assert.True(webhookRes.IsFailure);
        Assert.Equal("InvalidSignature", webhookRes.Error.Code);

        // Đảm bảo không thay đổi DB
        var checkOrder = await db.Orders.SingleAsync(o => o.Id == order.Id);
        Assert.Equal("Unpaid", checkOrder.OrderPaymentStatus);
        Assert.Equal("PendingPayment", checkOrder.FulfillmentStatus);
    }

    [Fact]
    public async Task Invariant9_DuplicateWebhookEventId_AcksSuccess_DoesNotCreateHistoryOrOutboxTwice()
    {
        var (scope, db, order, reg, clock) = CreateOrderContext(totalAmount: 390000m);
        using var _ = scope;

        var attemptHandler = new CreatePaymentAttemptCommandHandler(db, reg, clock);
        var attemptRes = await attemptHandler.Handle(new(factory.CustomerId, order.Id, Guid.NewGuid().ToString("N"), "fp"), CancellationToken.None);
        var merchantRef = attemptRes.Value.Attempt.MerchantReference;

        var now = clock.UtcNow;
        var eventId = $"EVT-DUP-{Guid.NewGuid():N}";
        var webhookPayload = new
        {
            eventId = eventId,
            merchantReference = merchantRef,
            amount = 390000m,
            currency = "VND",
            status = "Succeeded"
        };
        var rawBody = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(webhookPayload, JsonOptions));
        var signature = SandboxPaymentGateway.ComputeHmac(rawBody, SandboxPaymentGateway.DefaultSecret);

        var headers = new Dictionary<string, string>
        {
            [SandboxPaymentGateway.SignatureHeader] = signature,
            [SandboxPaymentGateway.TimestampHeader] = now.ToString("O")
        };

        var webhookHandler = new ProcessPaymentWebhookCommandHandler(db, reg, clock);

        // Lần 1: Thành công
        var res1 = await webhookHandler.Handle(new("Sandbox", rawBody, headers), CancellationToken.None);
        Assert.True(res1.IsSuccess);
        Assert.Equal("Applied", res1.Value.ProcessingResult);

        var initialHistCount = await db.OrderStatusHistories.CountAsync(h => h.OrderId == order.Id);
        var initialOutboxCount = await db.OutboxMessages.CountAsync(o => o.AggregateId == order.Id);

        // Lần 2: Gửi lại chính xác webhook đó (Duplicate)
        var res2 = await webhookHandler.Handle(new("Sandbox", rawBody, headers), CancellationToken.None);
        Assert.True(res2.IsSuccess);
        Assert.True(res2.Value.Acknowledged);

        var afterHistCount = await db.OrderStatusHistories.CountAsync(h => h.OrderId == order.Id);
        var afterOutboxCount = await db.OutboxMessages.CountAsync(o => o.AggregateId == order.Id);

        // Không tạo thêm lịch sử hoặc outbox lần 2
        Assert.Equal(initialHistCount, afterHistCount);
        Assert.Equal(initialOutboxCount, afterOutboxCount);
    }

    [Fact]
    public async Task Invariant10_WrongAmountOrCurrency_SetsNeedsAttention_DoesNotOpenProduction()
    {
        var (scope, db, order, reg, clock) = CreateOrderContext(totalAmount: 390000m);
        using var _ = scope;

        var attemptHandler = new CreatePaymentAttemptCommandHandler(db, reg, clock);
        var attemptRes = await attemptHandler.Handle(new(factory.CustomerId, order.Id, Guid.NewGuid().ToString("N"), "fp"), CancellationToken.None);
        var merchantRef = attemptRes.Value.Attempt.MerchantReference;

        var now = clock.UtcNow;
        // Gửi số tiền sai: 10.000 VNĐ thay vì 390.000 VNĐ
        var webhookPayload = new
        {
            eventId = $"EVT-WRONG-{Guid.NewGuid():N}",
            merchantReference = merchantRef,
            amount = 10000m,
            currency = "VND",
            status = "Succeeded"
        };
        var rawBody = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(webhookPayload, JsonOptions));
        var signature = SandboxPaymentGateway.ComputeHmac(rawBody, SandboxPaymentGateway.DefaultSecret);

        var headers = new Dictionary<string, string>
        {
            [SandboxPaymentGateway.SignatureHeader] = signature,
            [SandboxPaymentGateway.TimestampHeader] = now.ToString("O")
        };

        var webhookHandler = new ProcessPaymentWebhookCommandHandler(db, reg, clock);
        var res = await webhookHandler.Handle(new("Sandbox", rawBody, headers), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("NeedsAttention", res.Value.ProcessingResult);

        var updatedOrder = await db.Orders.SingleAsync(o => o.Id == order.Id);
        Assert.Equal("NeedsAttention", updatedOrder.OrderPaymentStatus);
        // Tuyệt đối không mở sang AwaitingAssignment
        Assert.Equal("PendingPayment", updatedOrder.FulfillmentStatus);

        var outboxDiscrepancy = await db.OutboxMessages.FirstOrDefaultAsync(o => o.EventType == "PaymentDiscrepancyDetected" && o.AggregateId == order.Id);
        Assert.NotNull(outboxDiscrepancy);
    }

    [Fact]
    public async Task Invariant11_FailedOrPendingEvent_ArrivingAfterPaid_DoesNotDowngradePaid()
    {
        var (scope, db, order, reg, clock) = CreateOrderContext("AwaitingAssignment", "Paid", totalAmount: 390000m);
        using var _ = scope;

        // Tạo attempt đã có
        var attempt = PaymentAttempt.Create(
            order.Id,
            "Sandbox",
            $"TB-REF-{Guid.NewGuid():N}",
            order.TotalAmount,
            order.Currency,
            clock.UtcNow.AddMinutes(15),
            clock.UtcNow
        );
        attempt.MarkSucceeded("TX-PAID", clock.UtcNow);
        db.PaymentAttempts.Add(attempt);
        await db.SaveChangesAsync();

        var now = clock.UtcNow;
        // Event lỗi gửi sau khi đã Paid
        var webhookPayload = new
        {
            eventId = $"EVT-LATE-FAIL-{Guid.NewGuid():N}",
            merchantReference = attempt.MerchantReference,
            amount = 390000m,
            currency = "VND",
            status = "Failed"
        };
        var rawBody = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(webhookPayload, JsonOptions));
        var signature = SandboxPaymentGateway.ComputeHmac(rawBody, SandboxPaymentGateway.DefaultSecret);

        var headers = new Dictionary<string, string>
        {
            [SandboxPaymentGateway.SignatureHeader] = signature,
            [SandboxPaymentGateway.TimestampHeader] = now.ToString("O")
        };

        var webhookHandler = new ProcessPaymentWebhookCommandHandler(db, reg, clock);
        var res = await webhookHandler.Handle(new("Sandbox", rawBody, headers), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("Ignored", res.Value.ProcessingResult);

        var checkOrder = await db.Orders.SingleAsync(o => o.Id == order.Id);
        // Không bị hạ Paid về Unpaid!
        Assert.Equal("Paid", checkOrder.OrderPaymentStatus);
        Assert.Equal("AwaitingAssignment", checkOrder.FulfillmentStatus);
    }

    [Fact]
    public async Task Invariant12_LateSuccessEvent_AfterOrderCancelled_SetsRefundPending_DoesNotOpenProduction()
    {
        var (scope, db, order, reg, clock) = CreateOrderContext("Cancelled", "Unpaid", totalAmount: 390000m);
        using var _ = scope;

        var attempt = PaymentAttempt.Create(
            order.Id,
            "Sandbox",
            $"TB-REF-CANCEL-{Guid.NewGuid():N}",
            order.TotalAmount,
            order.Currency,
            clock.UtcNow.AddMinutes(15),
            clock.UtcNow
        );
        db.PaymentAttempts.Add(attempt);
        await db.SaveChangesAsync();

        var now = clock.UtcNow;
        var webhookPayload = new
        {
            eventId = $"EVT-LATE-CANCEL-{Guid.NewGuid():N}",
            merchantReference = attempt.MerchantReference,
            amount = 390000m,
            currency = "VND",
            status = "Succeeded"
        };
        var rawBody = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(webhookPayload, JsonOptions));
        var signature = SandboxPaymentGateway.ComputeHmac(rawBody, SandboxPaymentGateway.DefaultSecret);

        var headers = new Dictionary<string, string>
        {
            [SandboxPaymentGateway.SignatureHeader] = signature,
            [SandboxPaymentGateway.TimestampHeader] = now.ToString("O")
        };

        var webhookHandler = new ProcessPaymentWebhookCommandHandler(db, reg, clock);
        var res = await webhookHandler.Handle(new("Sandbox", rawBody, headers), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("NeedsAttention", res.Value.ProcessingResult);

        var checkOrder = await db.Orders.SingleAsync(o => o.Id == order.Id);
        // Đưa vào RefundPending để đối soát
        Assert.Equal("RefundPending", checkOrder.OrderPaymentStatus);
        // Tuyệt đối không mở AwaitingAssignment cho đơn đã hủy
        Assert.Equal("Cancelled", checkOrder.FulfillmentStatus);
    }

    [Fact]
    public async Task Invariant13_ReconcileCommand_OnNeedsAttentionAttempt_ReconcilesWithoutNewCharge()
    {
        var (scope, db, order, reg, clock) = CreateOrderContext("PendingPayment", "Unpaid", totalAmount: 390000m);
        using var _ = scope;

        var attempt = PaymentAttempt.Create(
            order.Id,
            "Sandbox",
            $"TB-REF-RECONCILE-{Guid.NewGuid():N}",
            order.TotalAmount,
            order.Currency,
            clock.UtcNow.AddMinutes(15),
            clock.UtcNow
        );
        attempt.MarkNeedsAttention("Simulated network glitch");
        db.PaymentAttempts.Add(attempt);
        await db.SaveChangesAsync();

        var reconcileHandler = new ReconcilePaymentAttemptCommandHandler(db, reg, clock);
        var cmd = new ReconcilePaymentAttemptCommand(attempt.Id, factory.StaffId);
        var res = await reconcileHandler.Handle(cmd, CancellationToken.None);

        Assert.True(res.IsSuccess);

        var updatedOrder = await db.Orders.SingleAsync(o => o.Id == order.Id);
        Assert.Equal("Paid", updatedOrder.OrderPaymentStatus);
        Assert.Equal("AwaitingAssignment", updatedOrder.FulfillmentStatus);
    }

    [Fact]
    public async Task Invariant14_OfflineSandboxGateway_ContractTest()
    {
        var clock = new TheBoys.Infrastructure.Services.SystemClock();
        var gateway = new SandboxPaymentGateway(clock);

        Assert.Equal("Sandbox", gateway.GatewayName);
        Assert.True(gateway.IsConfigured);

        // 1. Create checkout
        var checkoutReq = new CreateCheckoutRequest(
            Guid.NewGuid(),
            "TB-OFFLINE-001",
            390000m,
            "VND",
            "Offline contract test",
            "https://test/return",
            "https://test/cancel"
        );
        var checkoutRes = await gateway.CreateCheckoutAsync(checkoutReq, CancellationToken.None);
        Assert.True(checkoutRes.Success);
        Assert.NotNull(checkoutRes.CheckoutUrl);
        Assert.NotNull(checkoutRes.ProviderTransactionId);

        // 2. Webhook validation
        var now = clock.UtcNow;
        var payload = new
        {
            eventId = "EVT-CONTRACT-001",
            merchantReference = "TB-OFFLINE-001",
            providerTransactionId = "TX-001",
            amount = 390000m,
            currency = "VND",
            status = "Succeeded"
        };
        var rawBody = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, JsonOptions));
        var validSig = SandboxPaymentGateway.ComputeHmac(rawBody, SandboxPaymentGateway.DefaultSecret);

        // Valid
        var verifyRes = await gateway.VerifyWebhookAsync(new(
            "Sandbox",
            rawBody,
            new Dictionary<string, string>
            {
                [SandboxPaymentGateway.SignatureHeader] = validSig,
                [SandboxPaymentGateway.TimestampHeader] = now.ToString("O")
            }
        ), CancellationToken.None);
        Assert.True(verifyRes.IsValid);
        Assert.Equal("EVT-CONTRACT-001", verifyRes.EventId);
        Assert.Equal("TB-OFFLINE-001", verifyRes.MerchantReference);
        Assert.Equal(390000m, verifyRes.AmountVnd);

        // Invalid signature
        var invalidSigRes = await gateway.VerifyWebhookAsync(new(
            "Sandbox",
            rawBody,
            new Dictionary<string, string>
            {
                [SandboxPaymentGateway.SignatureHeader] = "wrong_hash",
                [SandboxPaymentGateway.TimestampHeader] = now.ToString("O")
            }
        ), CancellationToken.None);
        Assert.False(invalidSigRes.IsValid);

        // Expired replay window (> 5 minutes ago)
        var oldTimestamp = now.AddMinutes(-10).ToString("O");
        var expiredRes = await gateway.VerifyWebhookAsync(new(
            "Sandbox",
            rawBody,
            new Dictionary<string, string>
            {
                [SandboxPaymentGateway.SignatureHeader] = validSig,
                [SandboxPaymentGateway.TimestampHeader] = oldTimestamp
            }
        ), CancellationToken.None);
        Assert.False(expiredRes.IsValid);
        Assert.Contains("replay window", expiredRes.FailureReason);
    }
}
