using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Application.Common.Models;
using TheBoys.Application.Common.Payments;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;

namespace TheBoys.Application.Payments.Commands.ProcessPaymentWebhook;

public sealed class ProcessPaymentWebhookCommandHandler(
    IApplicationDbContext db,
    IPaymentGatewayRegistry gatewayRegistry,
    IClock clock,
    ICreditService? creditService = null) : IRequestHandler<ProcessPaymentWebhookCommand, Result<ProcessPaymentWebhookResult>>
{
    public async Task<Result<ProcessPaymentWebhookResult>> Handle(ProcessPaymentWebhookCommand request, CancellationToken ct)
    {
        // 1. Phân giải Gateway
        IPaymentGateway gateway;
        try
        {
            gateway = gatewayRegistry.ResolveGateway(request.Gateway);
        }
        catch (Exception)
        {
            return Result<ProcessPaymentWebhookResult>.Failure(Error.NotFound("GatewayNotFound", $"Không hỗ trợ gateway '{request.Gateway}'."));
        }

        // 2. Xác thực chữ ký và replay window
        var verification = await gateway.VerifyWebhookAsync(
            new WebhookVerificationRequest(request.Gateway, request.RawBody, request.Headers),
            ct
        );

        if (!verification.IsValid)
        {
            return Result<ProcessPaymentWebhookResult>.Failure(Error.Unauthorized("InvalidSignature", verification.FailureReason ?? "Chữ ký webhook không hợp lệ."));
        }

        // Hỗ trợ webhook ping / xác nhận kết nối từ trang quản trị cổng thanh toán (ví dụ PayOS orderCode: 123 hoặc VQRIO123)
        if (verification.IsTestWebhook ||
            string.Equals(verification.EventId, "123", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(verification.MerchantReference, "VQRIO123", StringComparison.OrdinalIgnoreCase))
        {
            return Result<ProcessPaymentWebhookResult>.Success(new(
                Acknowledged: true,
                Message: $"{gateway.GatewayName} test webhook acknowledged.",
                ProcessingResult: "WebhookTestAcknowledged"
            ));
        }

        var payloadHash = SHA256.HashData(request.RawBody);
        var now = clock.UtcNow;

        // 3. Kiểm tra tính trùng lặp của EventId theo gateway
        var existingEvent = await db.PaymentEvents
            .AsNoTracking()
            .SingleOrDefaultAsync(e => e.Gateway == gateway.GatewayName && e.EventId == verification.EventId, ct);

        if (existingEvent != null)
        {
            // Trả ACK thành công ngay mà không tạo side-effect mới
            return Result<ProcessPaymentWebhookResult>.Success(new(Acknowledged: true, Message: "Duplicate event acknowledged.", ProcessingResult: existingEvent.ProcessingResult.ToString()));
        }

        // 4. Mở transaction ngắn để đối soát và cập nhật trạng thái
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

        try
        {
            // Tìm PaymentAttempt theo Gateway và (MerchantReference hoặc ProviderTransactionId)
            var attempt = await db.PaymentAttempts
                .Include(pa => pa.Order)
                .SingleOrDefaultAsync(pa => pa.Gateway == gateway.GatewayName &&
                    (pa.MerchantReference == verification.MerchantReference ||
                     (verification.ProviderTransactionId != null && pa.ProviderTransactionId == verification.ProviderTransactionId)), ct);

            // Fallback nếu ngân hàng cắt bớt hoặc loại bỏ dấu gạch ngang (ví dụ PayOS cắt tối đa 25 ký tự)
            if (attempt == null && !string.IsNullOrWhiteSpace(verification.MerchantReference))
            {
                var cleanRef = verification.MerchantReference.Replace("-", "");
                var pendingAttempts = await db.PaymentAttempts
                    .Include(pa => pa.Order)
                    .Where(pa => pa.Gateway == gateway.GatewayName && pa.Status == PaymentAttemptStatus.Pending)
                    .ToListAsync(ct);

                attempt = pendingAttempts.FirstOrDefault(pa =>
                    pa.MerchantReference.Replace("-", "").StartsWith(cleanRef, StringComparison.OrdinalIgnoreCase) ||
                    cleanRef.StartsWith(pa.MerchantReference.Replace("-", ""), StringComparison.OrdinalIgnoreCase));
            }

            // Trích xuất description và orderCode từ raw payload để kiểm tra topup
            string? webhookDesc = null;
            string? webhookOrderCodeStr = null;
            try
            {
                using var doc = JsonDocument.Parse(request.RawBody);
                if (doc.RootElement.TryGetProperty("data", out var dataElem))
                {
                    if (dataElem.TryGetProperty("description", out var dProp))
                        webhookDesc = dProp.GetString();
                    if (dataElem.TryGetProperty("orderCode", out var ocProp))
                        webhookOrderCodeStr = ocProp.ToString();
                }
            }
            catch { }

            // Bất biến Topup: Nếu orderCode hoặc description bắt đầu bằng TOPUP_, nạp credit cho user
            var isTopup = (webhookDesc != null && webhookDesc.StartsWith("TOPUP_", StringComparison.OrdinalIgnoreCase)) ||
                          (webhookOrderCodeStr != null && webhookOrderCodeStr.StartsWith("TOPUP_", StringComparison.OrdinalIgnoreCase)) ||
                          (verification.MerchantReference != null && verification.MerchantReference.StartsWith("TOPUP_", StringComparison.OrdinalIgnoreCase)) ||
                          (attempt != null && attempt.MerchantReference.StartsWith("TOPUP_", StringComparison.OrdinalIgnoreCase));

            if (isTopup)
            {
                var topupRef = attempt?.MerchantReference ?? webhookDesc ?? verification.MerchantReference ?? "";
                var parts = topupRef.Split('_');
                var pkgId = parts.Length > 1 ? parts[1].ToLowerInvariant() : "starter";
                Guid targetUserId = Guid.Empty;
                if (parts.Length > 2 && Guid.TryParse(parts[2], out var parsedUid))
                {
                    targetUserId = parsedUid;
                }
                else if (attempt?.Order != null)
                {
                    targetUserId = attempt.Order.UserId;
                }

                int credits = pkgId switch
                {
                    "studio" => 30,
                    "creator" => 10,
                    _ => 3
                };
                if (verification.AmountVnd >= 290000m) credits = 30;
                else if (verification.AmountVnd >= 120000m) credits = 10;
                else if (verification.AmountVnd >= 50000m) credits = 3;

                if (targetUserId != Guid.Empty && string.Equals(verification.EventStatus, "Succeeded", StringComparison.OrdinalIgnoreCase))
                {
                    if (creditService != null)
                    {
                        var opKey = $"topup:payos:{verification.EventId ?? Guid.NewGuid().ToString("N")}";
                        await creditService.AddPurchasedCreditsAsync(
                            targetUserId,
                            credits,
                            opKey,
                            $"Nạp {credits} lượt tạo 3D qua PayOS ({pkgId})",
                            ct
                        );
                    }
                }

                if (attempt != null)
                {
                    if (string.Equals(verification.EventStatus, "Succeeded", StringComparison.OrdinalIgnoreCase))
                    {
                        attempt.MarkSucceeded(verification.ProviderTransactionId, verification.EventTimeUtc ?? now);
                        if (attempt.Order != null)
                        {
                            attempt.Order.OrderPaymentStatus = "Paid";
                            attempt.Order.Status = OrderStatus.Paid;
                            attempt.Order.PaidAtUtc = now;
                        }
                    }
                    else
                    {
                        attempt.MarkFailed(verification.FailureReason ?? "Gateway reported non-success payment.");
                    }
                }

                var topupEvent = PaymentEvent.Create(
                    attempt?.Id ?? Guid.NewGuid(),
                    gateway.GatewayName,
                    verification.EventId ?? Guid.NewGuid().ToString("N"),
                    payloadHash,
                    verification.EventTimeUtc ?? now,
                    now
                );
                topupEvent.MarkProcessed(
                    PaymentEventProcessingResult.Applied,
                    now,
                    $"Xử lý thành công nạp {credits} credits qua PayOS."
                );
                db.PaymentEvents.Add(topupEvent);

                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                return Result<ProcessPaymentWebhookResult>.Success(new(
                    Acknowledged: true,
                    Message: $"Top-up webhook processed. Granted {credits} credits.",
                    ProcessingResult: "Applied"
                ));
            }

            if (attempt == null)
            {
                await transaction.RollbackAsync(ct);
                return Result<ProcessPaymentWebhookResult>.Failure(Error.NotFound("AttemptNotFound", $"Không tìm thấy PaymentAttempt cho reference '{verification.MerchantReference}'."));
            }

            var order = attempt.Order;

            // Tạo bản ghi PaymentEvent trong inbox
            var paymentEvent = PaymentEvent.Create(
                attempt.Id,
                gateway.GatewayName,
                verification.EventId!,
                payloadHash,
                verification.EventTimeUtc ?? now,
                now
            );
            db.PaymentEvents.Add(paymentEvent);

            // Bất biến 1: Đơn hàng đã bị hủy trước đó (Late success after cancelled)
            if (string.Equals(order.FulfillmentStatus, "Cancelled", StringComparison.OrdinalIgnoreCase) ||
                order.Status == OrderStatus.Cancelled)
            {
                if (string.Equals(verification.EventStatus, "Succeeded", StringComparison.OrdinalIgnoreCase))
                {
                    attempt.MarkSucceeded(verification.ProviderTransactionId, now);
                    order.OrderPaymentStatus = "RefundPending";

                    paymentEvent.MarkProcessed(
                        PaymentEventProcessingResult.NeedsAttention,
                        now,
                        "Thanh toán thành công đến sau khi đơn đã bị hủy. Đưa vào RefundPending để đối soát."
                    );

                    var outbox = OutboxMessage.Create(
                        "LatePaymentOnCancelledOrder",
                        order.Id,
                        $"late-pay:{attempt.Id:N}",
                        JsonSerializer.Serialize(new { orderId = order.Id, attemptId = attempt.Id, amount = verification.AmountVnd }),
                        now
                    );
                    db.OutboxMessages.Add(outbox);

                    var audit = AuditLog.Create(
                        null,
                        "LatePaymentOnCancelled",
                        "Order",
                        order.Id,
                        verification.EventId ?? Guid.NewGuid().ToString("N"),
                        JsonSerializer.Serialize(new { attemptId = attempt.Id, amount = verification.AmountVnd }),
                        "Late payment on cancelled order",
                        now
                    );
                    db.AuditLogs.Add(audit);

                    await db.SaveChangesAsync(ct);
                    await transaction.CommitAsync(ct);
                    return Result<ProcessPaymentWebhookResult>.Success(new(Acknowledged: true, Message: "Payment recorded for cancelled order (RefundPending).", ProcessingResult: "NeedsAttention"));
                }
            }

            // Bất biến 2: Sai lệch số tiền hoặc loại tiền tệ (Discrepancy)
            if (verification.AmountVnd != order.TotalAmount ||
                !string.Equals(verification.Currency, order.Currency, StringComparison.OrdinalIgnoreCase))
            {
                attempt.MarkNeedsAttention($"Sai lệch số tiền/tiền tệ. Nhận: {verification.AmountVnd} {verification.Currency}, Yêu cầu: {order.TotalAmount} {order.Currency}");
                order.OrderPaymentStatus = "NeedsAttention";

                paymentEvent.MarkProcessed(
                    PaymentEventProcessingResult.NeedsAttention,
                    now,
                    "Amount or currency mismatch."
                );

                var outbox = OutboxMessage.Create(
                    "PaymentDiscrepancyDetected",
                    order.Id,
                    $"discrepancy:{attempt.Id:N}",
                    JsonSerializer.Serialize(new { orderId = order.Id, attemptId = attempt.Id, receivedAmount = verification.AmountVnd, expectedAmount = order.TotalAmount }),
                    now
                );
                db.OutboxMessages.Add(outbox);

                var audit = AuditLog.Create(
                    null,
                    "PaymentDiscrepancy",
                    "Order",
                    order.Id,
                    verification.EventId ?? Guid.NewGuid().ToString("N"),
                    JsonSerializer.Serialize(new { attemptId = attempt.Id, receivedAmount = verification.AmountVnd, expectedAmount = order.TotalAmount }),
                    "Amount mismatch detected",
                    now
                );
                db.AuditLogs.Add(audit);

                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return Result<ProcessPaymentWebhookResult>.Success(new(Acknowledged: true, Message: "Payment discrepancy recorded (NeedsAttention).", ProcessingResult: "NeedsAttention"));
            }

            // Bất biến 3: Đơn hàng đã ở trạng thái Paid trước đó
            if (string.Equals(order.OrderPaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
            {
                paymentEvent.MarkProcessed(
                    PaymentEventProcessingResult.Ignored,
                    now,
                    "Đơn hàng đã được đánh dấu Paid trước đó. Bỏ qua để chống duplicate."
                );

                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return Result<ProcessPaymentWebhookResult>.Success(new(Acknowledged: true, Message: "Order already paid. Event ignored.", ProcessingResult: "Ignored"));
            }

            // Bất biến 4: Thanh toán thành công hợp lệ (Order Unpaid -> Paid, Fulfillment -> AwaitingAssignment)
            if (string.Equals(verification.EventStatus, "Succeeded", StringComparison.OrdinalIgnoreCase))
            {
                attempt.MarkSucceeded(verification.ProviderTransactionId, verification.EventTimeUtc ?? now);
                order.OrderPaymentStatus = "Paid";
                order.FulfillmentStatus = "AwaitingAssignment";
                order.PaidAtUtc = now;
                order.Status = OrderStatus.Paid;

                var histPayment = OrderStatusHistory.Create(
                    order.Id,
                    axis: "Payment",
                    fromStatus: "Unpaid",
                    toStatus: "Paid",
                    actorUserId: null,
                    source: $"Gateway:{gateway.GatewayName}",
                    reason: $"Xác nhận thanh toán thành công từ {gateway.GatewayName} (TxId: {verification.ProviderTransactionId ?? "N/A"}).",
                    operationKey: $"pay-capture-{attempt.Id:N}",
                    createdAtUtc: now
                );
                db.OrderStatusHistories.Add(histPayment);

                var histFulfillment = OrderStatusHistory.Create(
                    order.Id,
                    axis: "Fulfillment",
                    fromStatus: "PendingPayment",
                    toStatus: "AwaitingAssignment",
                    actorUserId: null,
                    source: $"Gateway:{gateway.GatewayName}",
                    reason: "Đơn hàng đã thanh toán đủ tiền, chuyển sang hàng đợi phân công xưởng in.",
                    operationKey: $"pay-fulfill-{attempt.Id:N}",
                    createdAtUtc: now
                );
                db.OrderStatusHistories.Add(histFulfillment);

                var outbox = OutboxMessage.Create(
                    "PaymentConfirmed",
                    order.Id,
                    $"payment-confirmed:{attempt.Id:N}",
                    JsonSerializer.Serialize(new { orderId = order.Id, attemptId = attempt.Id, amount = verification.AmountVnd, gateway = gateway.GatewayName }),
                    now
                );
                db.OutboxMessages.Add(outbox);

                var audit = AuditLog.Create(
                    null,
                    "PaymentCaptured",
                    "Order",
                    order.Id,
                    verification.EventId ?? Guid.NewGuid().ToString("N"),
                    JsonSerializer.Serialize(new { attemptId = attempt.Id, amount = verification.AmountVnd, gateway = gateway.GatewayName }),
                    "Payment captured via webhook",
                    now
                );
                db.AuditLogs.Add(audit);

                paymentEvent.MarkProcessed(
                    PaymentEventProcessingResult.Applied,
                    now,
                    "Thanh toán thành công và đã ghi nhận vào đơn hàng."
                );

                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return Result<ProcessPaymentWebhookResult>.Success(new(Acknowledged: true, Message: "Payment captured successfully.", ProcessingResult: "Applied"));
            }

            // Bất biến 5: Gateway báo thất bại hoặc pending
            attempt.MarkFailed(verification.FailureReason ?? "Gateway reported non-success payment.");
            paymentEvent.MarkProcessed(
                PaymentEventProcessingResult.Applied,
                now,
                "Ghi nhận trạng thái thất bại từ gateway."
            );

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Result<ProcessPaymentWebhookResult>.Success(new(Acknowledged: true, Message: "Payment failure recorded.", ProcessingResult: "Applied"));
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(ct);

            // Xử lý race-condition nếu có 2 webhook đồng thời gửi cùng EventId
            for (var retry = 0; retry < 5; retry++)
            {
                var dupEvent = await db.PaymentEvents
                    .AsNoTracking()
                    .SingleOrDefaultAsync(e => e.Gateway == gateway.GatewayName && e.EventId == verification.EventId, ct);

                if (dupEvent != null)
                {
                    return Result<ProcessPaymentWebhookResult>.Success(new(Acknowledged: true, Message: "Concurrent duplicate event acknowledged.", ProcessingResult: dupEvent.ProcessingResult.ToString()));
                }

                await Task.Delay(50, ct);
            }

            return Result<ProcessPaymentWebhookResult>.Success(new(Acknowledged: true, Message: "Concurrent duplicate event acknowledged.", ProcessingResult: "Applied"));
        }
    }
}

