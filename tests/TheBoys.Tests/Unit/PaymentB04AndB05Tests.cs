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
using TheBoys.Application.Orders.Commands.CancelOrderV2;
using TheBoys.Application.Orders.Commands.ReviewCancellationRequest;
using TheBoys.Application.Payments.Commands.CreatePaymentAttempt;
using TheBoys.Application.Payments.Commands.CreateRefund;
using TheBoys.Application.Payments.Commands.ProcessPaymentWebhook;
using TheBoys.Application.Payments.Commands.ReconcilePaymentAttempt;
using TheBoys.Application.Payments.Commands.ReconcileRefund;
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

    [Fact]
    public async Task Invariant15_Refund_CannotExceedCapturedAmount_EvenWithMultiplePartialRefunds()
    {
        var (scope, db, order, reg, clock) = CreateOrderContext("AwaitingAssignment", "Paid", totalAmount: 390000m);
        using var _ = scope;

        var attempt = PaymentAttempt.Create(order.Id, "Sandbox", $"REF-INV15-{Guid.NewGuid():N}", 390000m, "VND", clock.UtcNow.AddMinutes(30), clock.UtcNow);
        attempt.MarkSucceeded($"TX-INV15-{Guid.NewGuid():N}", clock.UtcNow);
        db.PaymentAttempts.Add(attempt);
        await db.SaveChangesAsync();

        var handler = new CreateRefundCommandHandler(db, reg, clock);

        // 1. First partial refund of 200,000 VND -> Success
        var ref1Cmd = new CreateRefundCommand(order.Id, factory.StaffId, Guid.NewGuid(), 200000m, "First partial refund", true);
        var ref1Res = await handler.Handle(ref1Cmd, CancellationToken.None);
        Assert.True(ref1Res.IsSuccess);
        Assert.False(ref1Res.Value.IsReplay);
        Assert.Equal("Succeeded", ref1Res.Value.Refund.Status);
        Assert.Equal(200000m, ref1Res.Value.Refund.AmountVnd);

        var refreshedOrder = await db.Orders.FindAsync(order.Id);
        Assert.Equal("PartiallyRefunded", refreshedOrder!.OrderPaymentStatus);

        // 2. Second refund of 250,000 VND -> Exceeds remaining 190,000 -> Fails with RefundExceedsCapturedAmount
        var ref2Cmd = new CreateRefundCommand(order.Id, factory.StaffId, Guid.NewGuid(), 250000m, "Over-limit refund", true);
        var ref2Res = await handler.Handle(ref2Cmd, CancellationToken.None);
        Assert.True(ref2Res.IsFailure);
        Assert.Equal("RefundExceedsCapturedAmount", ref2Res.Error.Code);

        // 3. Third refund of exactly remaining 190,000 VND -> Success, order transitions to Refunded
        var ref3Cmd = new CreateRefundCommand(order.Id, factory.StaffId, Guid.NewGuid(), 190000m, "Final partial refund", true);
        var ref3Res = await handler.Handle(ref3Cmd, CancellationToken.None);
        Assert.True(ref3Res.IsSuccess);
        Assert.Equal("Succeeded", ref3Res.Value.Refund.Status);

        await db.Entry(refreshedOrder).ReloadAsync();
        Assert.Equal("Refunded", refreshedOrder.OrderPaymentStatus);

        // 4. Any further refund (e.g. 10,000 VND) is rejected as remaining is 0
        var ref4Cmd = new CreateRefundCommand(order.Id, factory.StaffId, Guid.NewGuid(), 10000m, "Extra refund", true);
        var ref4Res = await handler.Handle(ref4Cmd, CancellationToken.None);
        Assert.True(ref4Res.IsFailure);
        Assert.Equal("InvalidOrderPaymentStatus", ref4Res.Error.Code);
    }

    [Fact]
    public async Task Invariant16_Refund_ReplaySameOperationKey_ReturnsExistingRefund_NoDoubleRefund()
    {
        var (scope, db, order, reg, clock) = CreateOrderContext("AwaitingAssignment", "Paid", totalAmount: 390000m);
        using var _ = scope;

        var attempt = PaymentAttempt.Create(order.Id, "Sandbox", $"REF-INV16-{Guid.NewGuid():N}", 390000m, "VND", clock.UtcNow.AddMinutes(30), clock.UtcNow);
        attempt.MarkSucceeded($"TX-INV16-{Guid.NewGuid():N}", clock.UtcNow);
        db.PaymentAttempts.Add(attempt);
        await db.SaveChangesAsync();

        var handler = new CreateRefundCommandHandler(db, reg, clock);
        var opKey = Guid.NewGuid();

        // 1. Customer gọi hoàn tiền trực tiếp -> Bị từ chối Forbidden (401/403)
        var custCmd = new CreateRefundCommand(order.Id, factory.CustomerId, opKey, 390000m, "Customer try refund", false);
        var custRes = await handler.Handle(custCmd, CancellationToken.None);
        Assert.True(custRes.IsFailure);
        Assert.Equal("Forbidden", custRes.Error.Code);

        // 2. Staff gọi hoàn tiền hợp lệ lần đầu -> Success với IsReplay = false
        var staffCmd = new CreateRefundCommand(order.Id, factory.StaffId, opKey, 390000m, "Staff approved refund", true);
        var res1 = await handler.Handle(staffCmd, CancellationToken.None);
        Assert.True(res1.IsSuccess);
        Assert.False(res1.Value.IsReplay);
        Assert.Equal(390000m, res1.Value.Refund.AmountVnd);

        // 3. Replay cùng OperationKey và cùng tham số -> Success với IsReplay = true
        var res2 = await handler.Handle(staffCmd, CancellationToken.None);
        Assert.True(res2.IsSuccess);
        Assert.True(res2.Value.IsReplay);
        Assert.Equal(res1.Value.Refund.Id, res2.Value.Refund.Id);
        Assert.Equal(res1.Value.Refund.AmountVnd, res2.Value.Refund.AmountVnd);

        // 4. Replay cùng OperationKey nhưng khác số tiền -> 409 Conflict (IdempotencyMismatch)
        var mismatchCmd = new CreateRefundCommand(order.Id, factory.StaffId, opKey, 200000m, "Different amount", true);
        var mismatchRes = await handler.Handle(mismatchCmd, CancellationToken.None);
        Assert.True(mismatchRes.IsFailure);
        Assert.Equal("IdempotencyMismatch", mismatchRes.Error.Code);

        // 5. Replay cùng OperationKey nhưng khác OrderId -> 409 Conflict (IdempotencyMismatch)
        var diffOrderCmd = new CreateRefundCommand(Guid.NewGuid(), factory.StaffId, opKey, 390000m, "Different order", true);
        var diffOrderRes = await handler.Handle(diffOrderCmd, CancellationToken.None);
        Assert.True(diffOrderRes.IsFailure);
        Assert.Equal("IdempotencyMismatch", diffOrderRes.Error.Code);

        // 6. Verify exactly 1 Refund entity exists in DB
        var count = await db.Refunds.CountAsync(r => r.PaymentAttemptId == attempt.Id);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Invariant17_Refund_TimeoutSetsNeedsAttention_ReconcileSucceeds()
    {
        var (scope, db, order, reg, clock) = CreateOrderContext("AwaitingAssignment", "Paid", totalAmount: 390000m);
        using var _ = scope;

        var attempt = PaymentAttempt.Create(order.Id, "Sandbox", $"REF-INV17-{Guid.NewGuid():N}", 390000m, "VND", clock.UtcNow.AddMinutes(30), clock.UtcNow);
        attempt.MarkSucceeded($"TX-INV17-{Guid.NewGuid():N}", clock.UtcNow);
        db.PaymentAttempts.Add(attempt);
        await db.SaveChangesAsync();

        var createHandler = new CreateRefundCommandHandler(db, reg, clock);

        // 1. Trigger simulated refund timeout
        var refundCmd = new CreateRefundCommand(order.Id, factory.StaffId, Guid.NewGuid(), 390000m, "SIMULATE_REFUND_TIMEOUT for testing", true);
        var createRes = await createHandler.Handle(refundCmd, CancellationToken.None);

        Assert.True(createRes.IsSuccess);
        Assert.Equal("NeedsAttention", createRes.Value.Refund.Status);

        var refreshedOrder = await db.Orders.FindAsync(order.Id);
        Assert.Equal("NeedsAttention", refreshedOrder!.OrderPaymentStatus);

        // 2. Reconcile by Staff
        var reconcileHandler = new ReconcileRefundCommandHandler(db, reg, clock);
        var reconcileCmd = new ReconcileRefundCommand(createRes.Value.Refund.Id, factory.StaffId, "Manual gateway status check confirmed refund executed");
        var reconcileRes = await reconcileHandler.Handle(reconcileCmd, CancellationToken.None);

        Assert.True(reconcileRes.IsSuccess);
        Assert.Equal("Succeeded", reconcileRes.Value.Status);

        await db.Entry(refreshedOrder).ReloadAsync();
        Assert.Equal("Refunded", refreshedOrder.OrderPaymentStatus);
    }

    [Fact]
    public async Task Invariant18_CancelUnpaidOrder_TransitionsDirectlyToCancelled_ExpiresPaymentAttempts()
    {
        var (scope, db, order, _, clock) = CreateOrderContext("PendingPayment", "Unpaid", totalAmount: 390000m);
        using var _ = scope;

        var attempt = PaymentAttempt.Create(order.Id, "Sandbox", $"MR-INV18-{Guid.NewGuid():N}", 390000m, "VND", clock.UtcNow.AddMinutes(15), clock.UtcNow);
        db.PaymentAttempts.Add(attempt);
        await db.SaveChangesAsync();

        var handler = new CancelOrderV2CommandHandler(db, clock);
        var cancelCmd = new CancelOrderV2Command(order.Id, factory.CustomerId, "User changed mind before payment", false);
        var res = await handler.Handle(cancelCmd, CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("CancelledDirectly", res.Value.CancellationStatus);
        Assert.Equal("Cancelled", res.Value.FulfillmentStatus);
        Assert.False(res.Value.RefundRequired);

        // Attempt is marked Expired
        var refreshedAttempt = await db.PaymentAttempts.FindAsync(attempt.Id);
        Assert.Equal(PaymentAttemptStatus.Expired, refreshedAttempt!.Status);
    }

    [Fact]
    public async Task Invariant19_CancelPaidOrder_CreatesCancellationRequest_CustomerCannotDirectCancelIfPrinting()
    {
        // Case A: Customer tries to cancel order already in Printing -> Conflict
        var (scopeA, dbA, printingOrder, _, clockA) = CreateOrderContext("Printing", "Paid", totalAmount: 390000m);
        using var _a = scopeA;
        printingOrder.PrintingStartedAtUtc = clockA.UtcNow.AddHours(-1);
        await dbA.SaveChangesAsync();

        var handlerA = new CancelOrderV2CommandHandler(dbA, clockA);
        var cancelCmdA = new CancelOrderV2Command(printingOrder.Id, factory.CustomerId, "Trying to cancel while printing", false);
        var resA = await handlerA.Handle(cancelCmdA, CancellationToken.None);

        Assert.True(resA.IsFailure);
        Assert.Equal("CannotCancelInProduction", resA.Error.Code);

        // Case B: Customer requests cancel for Paid order in AwaitingAssignment -> Creates Pending request
        var (scopeB, dbB, awaitingOrder, _, clockB) = CreateOrderContext("AwaitingAssignment", "Paid", totalAmount: 390000m);
        using var _b = scopeB;

        var handlerB = new CancelOrderV2CommandHandler(dbB, clockB);
        var cancelCmdB = new CancelOrderV2Command(awaitingOrder.Id, factory.CustomerId, "Please cancel before lab begins", false);
        var resB = await handlerB.Handle(cancelCmdB, CancellationToken.None);

        Assert.True(resB.IsSuccess);
        Assert.Equal("PendingReview", resB.Value.CancellationStatus);
        Assert.NotNull(resB.Value.CancellationRequestId);

        // Case C: Staff reviews and approves the request
        var reviewHandler = new ReviewCancellationRequestCommandHandler(dbB, clockB);
        var reviewCmd = new ReviewCancellationRequestCommand(resB.Value.CancellationRequestId!.Value, factory.StaffId, true, "Approved for refund");
        var reviewRes = await reviewHandler.Handle(reviewCmd, CancellationToken.None);

        Assert.True(reviewRes.IsSuccess);
        Assert.Equal("Approved", reviewRes.Value.Status);

        var refreshedOrderB = await dbB.Orders.FindAsync(awaitingOrder.Id);
        Assert.Equal("Cancelled", refreshedOrderB!.FulfillmentStatus);
        Assert.Equal("RefundPending", refreshedOrderB.OrderPaymentStatus);
    }

    [Fact]
    public async Task Invariant20_LatePaymentOnCancelledOrder_CanBeRefunded_FulfillmentRemainsCancelled()
    {
        var (scope, db, order, reg, clock) = CreateOrderContext("Cancelled", "RefundPending", totalAmount: 390000m);
        using var _ = scope;

        var attempt = PaymentAttempt.Create(order.Id, "Sandbox", $"REF-INV20-{Guid.NewGuid():N}", 390000m, "VND", clock.UtcNow.AddMinutes(30), clock.UtcNow);
        attempt.MarkSucceeded($"TX-INV20-{Guid.NewGuid():N}", clock.UtcNow);
        db.PaymentAttempts.Add(attempt);
        await db.SaveChangesAsync();

        var refundHandler = new CreateRefundCommandHandler(db, reg, clock);
        var refundCmd = new CreateRefundCommand(order.Id, factory.StaffId, Guid.NewGuid(), null, "Refund customer for late payment on cancelled order", true);
        var res = await refundHandler.Handle(refundCmd, CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal("Succeeded", res.Value.Refund.Status);
        Assert.Equal(390000m, res.Value.Refund.AmountVnd);

        var refreshedOrder = await db.Orders.FindAsync(order.Id);
        Assert.Equal("Refunded", refreshedOrder!.OrderPaymentStatus);
        Assert.Equal("Cancelled", refreshedOrder.FulfillmentStatus);
    }

    [Fact]
    public async Task Invariant21_CreateRefund_ValidatesOrderPaymentStatus_AndRequiresStaffReasonForActiveOrder()
    {
        // 1. Order ở trạng thái Unpaid -> Từ chối hoàn tiền với InvalidOrderPaymentStatus
        var (scope1, db1, unpaidOrder, reg1, clock1) = CreateOrderContext("PendingPayment", "Unpaid", totalAmount: 390000m);
        using var _1 = scope1;
        var handler1 = new CreateRefundCommandHandler(db1, reg1, clock1);
        var cmd1 = new CreateRefundCommand(unpaidOrder.Id, factory.StaffId, Guid.NewGuid(), 390000m, "Staff note", true);
        var res1 = await handler1.Handle(cmd1, CancellationToken.None);
        Assert.True(res1.IsFailure);
        Assert.Equal("InvalidOrderPaymentStatus", res1.Error.Code);

        // 2. Order chưa hủy (active: AwaitingAssignment) nhưng không có lý do hoàn tiền cụ thể -> Bị từ chối
        var (scope2, db2, activeOrder, reg2, clock2) = CreateOrderContext("AwaitingAssignment", "Paid", totalAmount: 390000m);
        using var _2 = scope2;
        var attempt2 = PaymentAttempt.Create(activeOrder.Id, "Sandbox", $"REF-INV21-{Guid.NewGuid():N}", 390000m, "VND", clock2.UtcNow.AddMinutes(30), clock2.UtcNow);
        attempt2.MarkSucceeded($"TX-INV21-{Guid.NewGuid():N}", clock2.UtcNow);
        db2.PaymentAttempts.Add(attempt2);
        await db2.SaveChangesAsync();

        var handler2 = new CreateRefundCommandHandler(db2, reg2, clock2);
        var cmd2 = new CreateRefundCommand(activeOrder.Id, factory.StaffId, Guid.NewGuid(), 100000m, "", true);
        var res2 = await handler2.Handle(cmd2, CancellationToken.None);
        Assert.True(res2.IsFailure);
        Assert.Equal("RefundReasonRequiredForActiveOrder", res2.Error.Code);
    }
}
