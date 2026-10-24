using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;
using TheBoys.Infrastructure.Persistence;
using TheBoys.Infrastructure.Security;
using TheBoys.Infrastructure.Services;
using TheBoys.WebApi;

namespace TheBoys.Tests.Unit;

public class EvtB08AndObsA09Tests(DemoFactory factory) : IClassFixture<DemoFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    [Fact]
    public async Task Test1_OutboxDispatcher_ProcessesMessage_CreatesInAppNotification_SetsDeliveredAndProcessed()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var orderId = Guid.NewGuid();
        var orderCode = $"TB-OB-{Guid.NewGuid():N}";
        var order = new Order
        {
            Id = orderId,
            OrderCode = orderCode,
            UserId = factory.CustomerId,
            PricingPackageId = DatabaseInitializer.DemoPackageId,
            Status = OrderStatus.Paid,
            FulfillmentStatus = "AwaitingAssignment",
            OrderPaymentStatus = "Paid",
            Currency = "VND",
            Quantity = 1,
            UnitPriceSnapshot = 390000m,
            SubTotalVnd = 390000m,
            TotalAmount = 390000m,
            ReceiverName = "Customer Test",
            ReceiverPhone = "0901234567",
            StreetAddress = "123 Street",
            District = "District 1",
            City = "Ho Chi Minh",
            CreatedAtUtc = clock.UtcNow
        };
        db.Orders.Add(order);

        var outbox = OutboxMessage.Create(
            eventType: "OrderPaid",
            aggregateId: order.Id,
            deduplicationKey: $"test:order:paid:{order.Id}",
            payloadJson: JsonSerializer.Serialize(new
            {
                orderId = order.Id,
                userId = factory.CustomerId,
                orderCode = order.OrderCode,
                amountVnd = 390000m
            }, JsonOptions),
            utcNow: clock.UtcNow
        );
        db.OutboxMessages.Add(outbox);
        await db.SaveChangesAsync();

        var dispatcher = new OutboxDispatcher(db, clock, NullLogger<OutboxDispatcher>.Instance);
        var result = await dispatcher.DispatchPendingAsync(batchSize: 10);

        Assert.Equal(1, result.ProcessedCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal(0, result.DeadLetteredCount);

        var refreshedOutbox = await db.OutboxMessages.FindAsync(outbox.Id);
        Assert.NotNull(refreshedOutbox!.ProcessedAtUtc);
        Assert.Null(refreshedOutbox.LeaseOwner);

        var notification = await db.Notifications.FirstOrDefaultAsync(n => n.OutboxMessageId == outbox.Id);
        Assert.NotNull(notification);
        Assert.Equal(factory.CustomerId, notification.UserId);
        Assert.Equal(NotificationChannel.InApp, notification.Channel);
        Assert.Equal("Thanh toán thành công", notification.Title);
        Assert.Contains(orderCode, notification.Body);
        Assert.NotNull(notification.DeliveredAtUtc);
        Assert.Null(notification.ReadAtUtc);
    }

    [Fact]
    public async Task Test2_OutboxDispatcher_IdempotentOnDuplicateRun_DoesNotDuplicateNotification()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var orderId = Guid.NewGuid();
        var order = new Order
        {
            Id = orderId,
            OrderCode = $"TB-IDEM-{Guid.NewGuid():N}",
            UserId = factory.CustomerId,
            PricingPackageId = DatabaseInitializer.DemoPackageId,
            Status = OrderStatus.Paid,
            FulfillmentStatus = "Assigned",
            OrderPaymentStatus = "Paid",
            Currency = "VND",
            Quantity = 1,
            UnitPriceSnapshot = 390000m,
            SubTotalVnd = 390000m,
            TotalAmount = 390000m,
            ReceiverName = "Customer Test",
            ReceiverPhone = "0901234567",
            StreetAddress = "123 Street",
            District = "District 1",
            City = "Ho Chi Minh",
            CreatedAtUtc = clock.UtcNow
        };
        db.Orders.Add(order);

        var outbox = OutboxMessage.Create(
            eventType: "OrderAssignedToPartner",
            aggregateId: order.Id,
            deduplicationKey: $"test:assign:{order.Id}",
            payloadJson: JsonSerializer.Serialize(new
            {
                orderId = order.Id,
                userId = factory.CustomerId,
                orderCode = order.OrderCode
            }, JsonOptions),
            utcNow: clock.UtcNow
        );
        db.OutboxMessages.Add(outbox);
        await db.SaveChangesAsync();

        // Giả lập kịch bản: Notification đã được tạo trước đó (ví dụ worker bị restart ngay sau khi insert notification)
        var existingNotif = Notification.Create(
            userId: factory.CustomerId,
            outboxMessageId: outbox.Id,
            channel: NotificationChannel.InApp,
            title: "Đã phân công xưởng in",
            body: $"Đơn hàng #{order.OrderCode} đã được chuyển tới xưởng in đối tác.",
            utcNow: clock.UtcNow
        );
        existingNotif.MarkDelivered(clock.UtcNow);
        db.Notifications.Add(existingNotif);
        await db.SaveChangesAsync();

        // Dispatcher chạy lại -> không tạo notification thứ 2 trùng (UserId, OutboxMessageId, InApp)
        var dispatcher = new OutboxDispatcher(db, clock, NullLogger<OutboxDispatcher>.Instance);
        var result = await dispatcher.DispatchPendingAsync(batchSize: 10);

        Assert.Equal(1, result.ProcessedCount);

        var notifs = await db.Notifications.Where(n => n.OutboxMessageId == outbox.Id).ToListAsync();
        Assert.Single(notifs);

        var refreshedOutbox = await db.OutboxMessages.FindAsync(outbox.Id);
        Assert.NotNull(refreshedOutbox!.ProcessedAtUtc);
    }

    [Fact]
    public async Task Test3_OutboxMessage_Retry_ExponentialBackoff_AndDeadLetterThreshold()
    {
        var now = DateTime.UtcNow;
        var msg = OutboxMessage.Create("FailingEvent", Guid.NewGuid(), $"fail:{Guid.NewGuid():N}", "{}", now);

        Assert.Equal(0, msg.AttemptCount);
        Assert.Null(msg.DeadLetteredAtUtc);
        Assert.Equal(now, msg.AvailableAtUtc);

        // Attempt 1: backoff base 15s * 2^0 = 15s
        msg.RecordFailure(now, maxAttempts: 5, baseDelaySeconds: 15);
        Assert.Equal(1, msg.AttemptCount);
        Assert.Equal(now.AddSeconds(15), msg.AvailableAtUtc);
        Assert.Null(msg.DeadLetteredAtUtc);

        // Attempt 2: backoff 15s * 2^1 = 30s
        msg.RecordFailure(now, maxAttempts: 5, baseDelaySeconds: 15);
        Assert.Equal(2, msg.AttemptCount);
        Assert.Equal(now.AddSeconds(30), msg.AvailableAtUtc);

        // Attempt 3: backoff 15s * 2^2 = 60s
        msg.RecordFailure(now, maxAttempts: 5, baseDelaySeconds: 15);
        Assert.Equal(3, msg.AttemptCount);
        Assert.Equal(now.AddSeconds(60), msg.AvailableAtUtc);

        // Attempt 4: backoff 15s * 2^3 = 120s
        msg.RecordFailure(now, maxAttempts: 5, baseDelaySeconds: 15);
        Assert.Equal(4, msg.AttemptCount);
        Assert.Equal(now.AddSeconds(120), msg.AvailableAtUtc);

        // Attempt 5 (Chạm MaxAttempts = 5) -> Chuyển sang DeadLetteredAtUtc != null
        msg.RecordFailure(now, maxAttempts: 5, baseDelaySeconds: 15);
        Assert.Equal(5, msg.AttemptCount);
        Assert.NotNull(msg.DeadLetteredAtUtc);
        Assert.Equal(now, msg.DeadLetteredAtUtc.Value);
    }

    [Fact]
    public void Test4_PiiAndSecretRedactor_ScrubsSensitiveKeys_Tokens_AndPasswords()
    {
        var rawLog = "Config: MeshyKey=msy_test_secret_key_12345, TripoKey=tsk_super_secret_token, Body={\"password\": \"P@ssword123!\"} with Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.e30.t-IDcSemACt8x4iTMCda8Yhe3iZaWbvV5XKSTbuAn0M and ConnStr: Server=tcp:sql.db;User=app;Password=MySecretPassword123;";

        var redacted = PiiAndSecretRedactor.RedactText(rawLog);

        Assert.DoesNotContain("P@ssword123!", redacted);
        Assert.DoesNotContain("msy_test_secret_key_12345", redacted);
        Assert.DoesNotContain("tsk_super_secret_token", redacted);
        Assert.DoesNotContain("MySecretPassword123", redacted);
        Assert.DoesNotContain("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9", redacted);

        Assert.Contains("[REDACTED_API_KEY]", redacted);
        Assert.Contains("Bearer [REDACTED_TOKEN]", redacted);
        Assert.Contains("\"password\": \"[REDACTED]\"", redacted);
        Assert.Contains("Password=[REDACTED]", redacted);

        var masked = PiiAndSecretRedactor.MaskApiKey("msy_secret_api_key_xyz");
        Assert.Equal("msy_..._xyz", masked);
    }

    [Fact]
    public async Task Test5_PrivacyRequest_Lifecycle_EnforcesConstraint_CompletedRequiresCompletedAtUtc()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        // 1. Tạo yêu cầu PrivacyRequest loại DeleteAssets
        var request = PrivacyRequest.Create(
            userId: factory.CustomerId,
            requestType: PrivacyRequestType.DeleteAssets,
            reason: "Tôi muốn xóa toàn bộ file 3D và ảnh cá nhân",
            utcNow: clock.UtcNow
        );

        Assert.Equal(PrivacyRequestStatus.Pending, request.Status);
        Assert.Null(request.CompletedAtUtc);

        db.PrivacyRequests.Add(request);
        await db.SaveChangesAsync();

        // 2. Chuyển sang Processing
        request.TransitionToProcessing(factory.StaffId, "Đang xử lý dọn dẹp asset lưu trữ");
        Assert.Equal(PrivacyRequestStatus.Processing, request.Status);
        Assert.Equal(factory.StaffId, request.HandledByUserId);
        await db.SaveChangesAsync();

        // 3. Hoàn tất với Complete -> Thỏa mãn CK_PrivacyRequests_Completed
        var completedTime = clock.UtcNow;
        request.Complete(factory.StaffId, completedTime, "Đã xóa toàn bộ assets thành công");
        Assert.Equal(PrivacyRequestStatus.Completed, request.Status);
        Assert.Equal(completedTime, request.CompletedAtUtc);
        await db.SaveChangesAsync();

        var refreshed = await db.PrivacyRequests.FindAsync(request.Id);
        Assert.NotNull(refreshed);
        Assert.Equal(PrivacyRequestStatus.Completed, refreshed.Status);
        Assert.NotNull(refreshed.CompletedAtUtc);
    }
}
