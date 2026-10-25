using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TheBoys.Application.Common.Interfaces;
using TheBoys.WebApi.Security;
using TheBoys.Domain.Enums;
using TheBoys.Infrastructure.Security;

namespace TheBoys.WebApi.Controllers.v2;

public sealed record AiProviderMetricDto(
    string Provider,
    int TotalJobs,
    int SucceededJobs,
    int FailedJobs,
    int RunningJobs
);

public sealed record CommerceMetricDto(
    int TotalOrders,
    Dictionary<string, int> OrdersByFulfillmentStatus,
    Dictionary<string, int> OrdersByPaymentStatus,
    int PaymentAttemptsNeedsAttention,
    int RefundsNeedsAttention
);

public sealed record OutboxAndNotificationMetricDto(
    int OutboxPending,
    int OutboxProcessed,
    int OutboxDeadLettered,
    int NotificationsDelivered,
    int NotificationsRead
);

public sealed record ObservabilityReportDto(
    DateTime TimestampUtc,
    List<AiProviderMetricDto> AiGenerations,
    CommerceMetricDto Commerce,
    OutboxAndNotificationMetricDto Messaging,
    string SecurityFilterStatus
);

[ApiController, Route("api/v2/admin"), Authorize(Roles = Roles.Management)]
public sealed class ObservabilityV2Controller(
    IApplicationDbContext db,
    IClock clock
) : ApiControllerBase
{
    [HttpGet("observability")]
    public async Task<IActionResult> GetObservabilityReport(CancellationToken ct = default)
    {
        var now = clock.UtcNow;

        // 1. Thống kê AI Generation theo Provider
        var allJobs = await db.GenerationJobs
            .AsNoTracking()
            .Select(j => new
            {
                j.Provider,
                j.Status
            })
            .ToListAsync(ct);

        var aiMetrics = allJobs
            .GroupBy(j => j.Provider)
            .Select(g => new AiProviderMetricDto(
                Provider: g.Key,
                TotalJobs: g.Count(),
                SucceededJobs: g.Count(j => string.Equals(j.Status, "Succeeded", StringComparison.OrdinalIgnoreCase)),
                FailedJobs: g.Count(j => string.Equals(j.Status, "Failed", StringComparison.OrdinalIgnoreCase)),
                RunningJobs: g.Count(j => string.Equals(j.Status, "Running", StringComparison.OrdinalIgnoreCase) || string.Equals(j.Status, "Queued", StringComparison.OrdinalIgnoreCase))
            ))
            .ToList();

        // 2. Thống kê Commerce & Payment
        var orders = await db.Orders
            .AsNoTracking()
            .Select(o => new { o.FulfillmentStatus, o.OrderPaymentStatus })
            .ToListAsync(ct);

        var ordersByFulfillment = orders
            .GroupBy(o => o.FulfillmentStatus)
            .ToDictionary(g => g.Key, g => g.Count());

        var ordersByPayment = orders
            .GroupBy(o => o.OrderPaymentStatus)
            .ToDictionary(g => g.Key, g => g.Count());

        var paymentAttemptsAttention = await db.PaymentAttempts
            .AsNoTracking()
            .CountAsync(p => p.Status == PaymentAttemptStatus.NeedsAttention, ct);

        var refundsAttention = await db.Refunds
            .AsNoTracking()
            .CountAsync(r => r.Status == RefundStatus.NeedsAttention, ct);

        var commerce = new CommerceMetricDto(
            TotalOrders: orders.Count,
            OrdersByFulfillmentStatus: ordersByFulfillment,
            OrdersByPaymentStatus: ordersByPayment,
            PaymentAttemptsNeedsAttention: paymentAttemptsAttention,
            RefundsNeedsAttention: refundsAttention
        );

        // 3. Thống kê Outbox & Notification
        var outboxPending = await db.OutboxMessages
            .AsNoTracking()
            .CountAsync(m => m.ProcessedAtUtc == null && m.DeadLetteredAtUtc == null, ct);

        var outboxProcessed = await db.OutboxMessages
            .AsNoTracking()
            .CountAsync(m => m.ProcessedAtUtc != null, ct);

        var outboxDeadLettered = await db.OutboxMessages
            .AsNoTracking()
            .CountAsync(m => m.DeadLetteredAtUtc != null, ct);

        var notifsDelivered = await db.Notifications
            .AsNoTracking()
            .CountAsync(n => n.DeliveredAtUtc != null, ct);

        var notifsRead = await db.Notifications
            .AsNoTracking()
            .CountAsync(n => n.ReadAtUtc != null, ct);

        var messaging = new OutboxAndNotificationMetricDto(
            OutboxPending: outboxPending,
            OutboxProcessed: outboxProcessed,
            OutboxDeadLettered: outboxDeadLettered,
            NotificationsDelivered: notifsDelivered,
            NotificationsRead: notifsRead
        );

        var report = new ObservabilityReportDto(
            TimestampUtc: now,
            AiGenerations: aiMetrics,
            Commerce: commerce,
            Messaging: messaging,
            SecurityFilterStatus: PiiAndSecretRedactor.RedactText("PiiAndSecretRedactor Active. Secrets/Tokens are shielded.")
        );

        return Ok(report);
    }
}
