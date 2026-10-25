using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;

namespace TheBoys.Infrastructure.Services;

public sealed class OutboxDispatcher(
    IApplicationDbContext db,
    IClock clock,
    ILogger<OutboxDispatcher> logger
) : IOutboxDispatcher
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<OutboxDispatchBatchResult> DispatchPendingAsync(int batchSize = 50, CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var candidates = await db.OutboxMessages
            .Where(m => m.ProcessedAtUtc == null &&
                        m.DeadLetteredAtUtc == null &&
                        m.AvailableAtUtc <= now &&
                        (m.LeaseExpiresAtUtc == null || m.LeaseExpiresAtUtc <= now))
            .OrderBy(m => m.AvailableAtUtc)
            .Take(batchSize)
            .ToListAsync(ct);

        if (candidates.Count == 0)
        {
            return new OutboxDispatchBatchResult(0, 0, 0);
        }

        var leaseOwner = $"worker-{Environment.MachineName}-{Guid.NewGuid():N}";
        var leaseExpiry = now.AddMinutes(2);
        foreach (var msg in candidates)
        {
            msg.AcquireLease(leaseOwner, leaseExpiry);
        }
        await db.SaveChangesAsync(ct);

        int processed = 0;
        int failed = 0;
        int deadLettered = 0;

        foreach (var msg in candidates)
        {
            try
            {
                await ProcessSingleMessageAsync(msg, now, ct);
                msg.MarkProcessed(now);
                await db.SaveChangesAsync(ct);
                processed++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Lỗi khi xử lý OutboxMessage {MessageId} ({EventType})", msg.Id, msg.EventType);
                msg.RecordFailure(now, maxAttempts: 5, baseDelaySeconds: 15);
                await db.SaveChangesAsync(ct);

                if (msg.DeadLetteredAtUtc != null)
                {
                    deadLettered++;
                }
                else
                {
                    failed++;
                }
            }
        }

        return new OutboxDispatchBatchResult(processed, failed, deadLettered);
    }

    private async Task ProcessSingleMessageAsync(OutboxMessage msg, DateTime now, CancellationToken ct)
    {
        Guid targetUserId = Guid.Empty;
        string orderCode = string.Empty;
        string carrier = string.Empty;
        string trackingNumber = string.Empty;
        decimal? amountVnd = null;

        // Parse Payload JSON
        try
        {
            using var doc = JsonDocument.Parse(msg.PayloadJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("userId", out var uProp) && uProp.TryGetGuid(out var uId))
            {
                targetUserId = uId;
            }

            if (root.TryGetProperty("orderCode", out var codeProp))
            {
                orderCode = codeProp.GetString() ?? string.Empty;
            }

            if (root.TryGetProperty("carrier", out var carrProp))
            {
                carrier = carrProp.GetString() ?? string.Empty;
            }

            if (root.TryGetProperty("trackingNumber", out var trackProp))
            {
                trackingNumber = trackProp.GetString() ?? string.Empty;
            }

            if (root.TryGetProperty("amountVnd", out var amtProp) && amtProp.TryGetDecimal(out var amt))
            {
                amountVnd = amt;
            }

            // Nếu chưa có targetUserId, thử đọc từ orderId
            if (targetUserId == Guid.Empty && root.TryGetProperty("orderId", out var oProp) && oProp.TryGetGuid(out var oId))
            {
                var ord = await db.Orders
                    .AsNoTracking()
                    .Where(o => o.Id == oId)
                    .Select(o => new { o.UserId, o.OrderCode })
                    .FirstOrDefaultAsync(ct);

                if (ord != null)
                {
                    targetUserId = ord.UserId;
                    if (string.IsNullOrEmpty(orderCode)) orderCode = ord.OrderCode;
                }
            }
        }
        catch (JsonException)
        {
            // Bỏ qua lỗi parse JSON, dùng aggregateId
        }

        // Nếu vẫn chưa có targetUserId, thử tìm Order bằng AggregateId
        if (targetUserId == Guid.Empty)
        {
            var ord = await db.Orders
                .AsNoTracking()
                .Where(o => o.Id == msg.AggregateId)
                .Select(o => new { o.UserId, o.OrderCode })
                .FirstOrDefaultAsync(ct);

            if (ord != null)
            {
                targetUserId = ord.UserId;
                if (string.IsNullOrEmpty(orderCode)) orderCode = ord.OrderCode;
            }
        }

        // Suy diễn Title & Body tiếng Việt theo EventType
        var (title, body) = BuildNotificationContent(msg.EventType, orderCode, carrier, trackingNumber, amountVnd);

        if (targetUserId != Guid.Empty)
        {
            // Idempotency / Deduplication: Kiểm tra đã có notification (UserId, OutboxMessageId, InApp) chưa
            var exists = await db.Notifications.AnyAsync(n =>
                n.UserId == targetUserId &&
                n.OutboxMessageId == msg.Id &&
                n.Channel == NotificationChannel.InApp, ct);

            if (!exists)
            {
                var notif = Notification.Create(
                    userId: targetUserId,
                    outboxMessageId: msg.Id,
                    channel: NotificationChannel.InApp,
                    title: title,
                    body: body,
                    utcNow: now
                );
                notif.MarkDelivered(now);
                db.Notifications.Add(notif);
            }
        }
    }

    private static (string Title, string Body) BuildNotificationContent(
        string eventType,
        string orderCode,
        string carrier,
        string trackingNumber,
        decimal? amountVnd)
    {
        var codeDisplay = string.IsNullOrWhiteSpace(orderCode) ? "của bạn" : $"#{orderCode}";

        return eventType switch
        {
            "OrderCreated" => (
                "Đơn hàng mới",
                $"Đơn hàng {codeDisplay} đã được khởi tạo thành công."
            ),
            "OrderPaid" => (
                "Thanh toán thành công",
                $"Đơn hàng {codeDisplay} đã được thanh toán thành công."
            ),
            "OrderAssignedToPartner" or "PartnerAssignmentOffered" => (
                "Đã phân công xưởng in",
                $"Đơn hàng {codeDisplay} đã được chuyển tới xưởng in đối tác."
            ),
            "PartnerAssignmentAccepted" => (
                "Xưởng đã nhận đơn",
                $"Xưởng in đã tiếp nhận và chuẩn bị in cho đơn hàng {codeDisplay}."
            ),
            "ProductionRunStarted" => (
                "Đang tiến hành in 3D",
                $"Đơn hàng {codeDisplay} đang được in tại xưởng."
            ),
            "OrderPassedQcAndPackaged" => (
                "Đã hoàn thành in & đóng gói",
                $"Sản phẩm của đơn hàng {codeDisplay} đã đạt chuẩn kiểm định chất lượng và đóng gói sẵn sàng giao."
            ),
            "ShipmentCreated" => (
                "Đơn hàng đang được giao",
                $"Đơn hàng {codeDisplay} đang được vận chuyển bởi {carrier} (Mã vận đơn: {trackingNumber})."
            ),
            "OrderDelivered" => (
                "Giao hàng thành công",
                $"Đơn hàng {codeDisplay} đã được giao thành công đến bạn."
            ),
            "DefectReportCreated" => (
                "Tiếp nhận khiếu nại sản phẩm",
                $"Khiếu nại cho đơn hàng {codeDisplay} đã được tiếp nhận và ban quản lý đang xử lý."
            ),
            "DefectReprintApproved" => (
                "Chấp thuận in lại bảo hành",
                $"Khiếu nại đơn hàng {codeDisplay} đã được duyệt in lại bảo hành miễn phí."
            ),
            "DefectRefundApproved" => (
                "Chấp thuận hoàn tiền khiếu nại",
                $"Khiếu nại đơn hàng {codeDisplay} đã được duyệt hoàn tiền."
            ),
            "OrderRefunded" => (
                "Hoàn tiền thành công",
                $"Khoản tiền {(amountVnd.HasValue ? $"{amountVnd.Value:N0} VNĐ" : string.Empty)} của đơn hàng {codeDisplay} đã được hoàn trả thành công."
            ),
            _ => (
                $"Thông báo {eventType}",
                $"Cập nhật mới cho đơn hàng {codeDisplay}."
            )
        };
    }
}

