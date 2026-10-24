using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Application.Common.Models;
using TheBoys.Application.Common.Payments;
using TheBoys.Application.Payments.DTOs;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;

namespace TheBoys.Application.Payments.Commands.CreateRefund;

public sealed record CreateRefundResult(RefundDto Refund, bool IsReplay);

public sealed record CreateRefundCommand(
    Guid OrderId,
    Guid RequestedByUserId,
    Guid OperationKey,
    decimal? AmountVnd,
    string Reason,
    bool IsManagement
) : IRequest<Result<CreateRefundResult>>;

public sealed class CreateRefundCommandHandler(
    IApplicationDbContext db,
    IPaymentGatewayRegistry gatewayRegistry,
    IClock clock
) : IRequestHandler<CreateRefundCommand, Result<CreateRefundResult>>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task<Result<CreateRefundResult>> Handle(CreateRefundCommand command, CancellationToken ct)
    {
        // 1. Phân quyền: Tuyệt đối không cho Customer tự gọi hoàn tiền
        if (!command.IsManagement)
        {
            return Result<CreateRefundResult>.Failure(Error.Unauthorized("Forbidden", "Chỉ quản trị viên hoặc nhân viên mới có quyền hoàn tiền."));
        }

        // 2. Idempotency check trước ngoài transaction: Replay cùng OperationKey
        var existingRefund = await db.Refunds
            .Include(r => r.PaymentAttempt)
            .SingleOrDefaultAsync(r => r.OperationKey == command.OperationKey, ct);

        if (existingRefund != null)
        {
            if (existingRefund.PaymentAttempt.OrderId != command.OrderId ||
                (command.AmountVnd.HasValue && command.AmountVnd.Value != existingRefund.AmountVnd))
            {
                return Result<CreateRefundResult>.Failure(Error.Conflict(
                    "IdempotencyMismatch",
                    "Idempotency-Key đã được sử dụng cho một giao dịch hoàn tiền khác với thông tin không trùng khớp."
                ));
            }

            return Result<CreateRefundResult>.Success(new CreateRefundResult(MapToDto(existingRefund), IsReplay: true));
        }

        // 3. Kiểm tra Order & điều kiện trạng thái đơn
        var order = await db.Orders
            .Include(o => o.CancellationRequests)
            .Include(o => o.DefectReports)
            .SingleOrDefaultAsync(o => o.Id == command.OrderId, ct);

        if (order == null)
            return Result<CreateRefundResult>.Failure(Error.NotFound("OrderNotFound", "Không tìm thấy đơn hàng."));

        var validPaymentStatuses = new[] { "RefundPending", "Paid", "PartiallyRefunded" };
        if (!validPaymentStatuses.Contains(order.OrderPaymentStatus))
        {
            return Result<CreateRefundResult>.Failure(Error.Conflict(
                "InvalidOrderPaymentStatus",
                $"Không thể hoàn tiền cho đơn hàng có trạng thái thanh toán '{order.OrderPaymentStatus}'."
            ));
        }

        if (order.FulfillmentStatus != "Cancelled" && (string.IsNullOrWhiteSpace(command.Reason) || command.Reason.Trim() == "Khách hàng / Quản trị viên yêu cầu hoàn tiền"))
        {
            return Result<CreateRefundResult>.Failure(Error.Conflict(
                "RefundReasonRequiredForActiveOrder",
                "Đơn hàng đang hoạt động (chưa hủy) yêu cầu phải có lý do hoàn tiền cụ thể từ nhân viên."
            ));
        }

        var now = clock.UtcNow;
        Refund refund;
        PaymentAttempt attempt;

        // BƯỚC 1: RESERVE — Khóa hạn mức bằng Transaction & Application Lock trước khi gọi gateway ngoài
        await using (var tx = await db.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct))
        {
            // Khóa ứng dụng cấp transaction theo OrderId NGAY TẠI ĐẦU TRANSACTION để ngăn chặn race condition trước khi thực hiện bất kỳ truy vấn SELECT nào
            var lockAcquired = await db.AcquireApplicationLockAsync($"Refund_Order_{command.OrderId}", TimeSpan.FromSeconds(15), ct);
            if (!lockAcquired)
            {
                await tx.RollbackAsync(ct);
                return Result<CreateRefundResult>.Failure(Error.Conflict(
                    "RefundLockConflict",
                    "Đơn hàng đang có giao dịch hoàn tiền khác đang được xử lý, vui lòng thử lại sau."
                ));
            }

            var foundAttempt = await db.PaymentAttempts
                .Include(a => a.Refunds)
                .SingleOrDefaultAsync(a => a.OrderId == command.OrderId && a.Status == PaymentAttemptStatus.Succeeded, ct);

            if (foundAttempt == null)
            {
                await tx.RollbackAsync(ct);
                return Result<CreateRefundResult>.Failure(Error.Conflict(
                    "NoSucceededPayment",
                    "Đơn hàng chưa có giao dịch thanh toán thành công để hoàn tiền."
                ));
            }

            attempt = foundAttempt;

            // Kiểm tra lại Idempotency bên trong lock đề phòng concurrent replay
            var raceExisting = await db.Refunds
                .Include(r => r.PaymentAttempt)
                .SingleOrDefaultAsync(r => r.OperationKey == command.OperationKey, ct);

            if (raceExisting != null)
            {
                await tx.RollbackAsync(ct);
                if (raceExisting.PaymentAttempt.OrderId != command.OrderId ||
                    (command.AmountVnd.HasValue && command.AmountVnd.Value != raceExisting.AmountVnd))
                {
                    return Result<CreateRefundResult>.Failure(Error.Conflict(
                        "IdempotencyMismatch",
                        "Idempotency-Key đã được sử dụng cho một giao dịch hoàn tiền khác với thông tin không trùng khớp."
                    ));
                }
                return Result<CreateRefundResult>.Success(new CreateRefundResult(MapToDto(raceExisting), IsReplay: true));
            }

            // Tính toán hạn mức hoàn tiền còn lại (Capturable Amount Invariant)
            var activeRefunds = await db.Refunds
                .Where(r => r.PaymentAttemptId == attempt.Id &&
                           (r.Status == RefundStatus.Reserved ||
                            r.Status == RefundStatus.Submitted ||
                            r.Status == RefundStatus.Succeeded ||
                            r.Status == RefundStatus.NeedsAttention))
                .ToListAsync(ct);

            var activeRefundedAmount = activeRefunds.Sum(r => r.AmountVnd);
            var remainingCapturable = attempt.AmountVnd - activeRefundedAmount;
            var refundAmount = command.AmountVnd ?? remainingCapturable;

            if (refundAmount <= 0)
            {
                await tx.RollbackAsync(ct);
                return Result<CreateRefundResult>.Failure(Error.Conflict("InvalidRefundAmount", "Số tiền hoàn phải lớn hơn 0."));
            }

            if (refundAmount > remainingCapturable)
            {
                await tx.RollbackAsync(ct);
                return Result<CreateRefundResult>.Failure(Error.Conflict(
                    "RefundExceedsCapturedAmount",
                    $"Số tiền hoàn yêu cầu ({refundAmount:N0} VNĐ) vượt quá số tiền có thể hoàn còn lại ({remainingCapturable:N0} VNĐ)."
                ));
            }

            refund = Refund.Create(
                paymentAttemptId: attempt.Id,
                requestedByUserId: command.RequestedByUserId,
                operationKey: command.OperationKey,
                amountVnd: refundAmount,
                reason: command.Reason,
                createdAtUtc: now
            );
            refund.MarkReserved();
            db.Refunds.Add(refund);

            // Liên kết với yêu cầu hủy đơn nếu có
            var approvedCancellation = order.CancellationRequests
                .FirstOrDefault(c => c.Status == CancellationRequestStatus.Approved && c.RefundId == null);
            if (approvedCancellation != null)
            {
                approvedCancellation.RefundId = refund.Id;
            }

            // Liên kết với khiếu nại đã duyệt hoàn tiền nếu có (OPS-B07)
            var approvedDefect = order.DefectReports
                .FirstOrDefault(d => d.Status == DefectReportStatus.RefundApproved && d.RefundId == null && d.ReprintRunId == null);
            if (approvedDefect != null)
            {
                approvedDefect.RefundId = refund.Id;
            }

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }

        // BƯỚC 2: SUBMIT — Đánh dấu Submitted và gọi cổng thanh toán ngoài (sau khi đã reserve chắc chắn trong DB)
        refund.MarkSubmitted();
        await db.SaveChangesAsync(ct);

        var gateway = gatewayRegistry.ResolveGateway(attempt.Gateway);
        var submitReq = new SubmitRefundRequest(
            OrderId: order.Id,
            PaymentAttemptId: attempt.Id,
            OperationKey: refund.OperationKey,
            MerchantReference: attempt.MerchantReference,
            ProviderTransactionId: attempt.ProviderTransactionId,
            AmountVnd: refund.AmountVnd,
            Currency: attempt.Currency,
            Reason: refund.Reason
        );

        SubmitRefundResult submitResult;
        try
        {
            submitResult = await gateway.SubmitRefundAsync(submitReq, ct);
        }
        catch (Exception ex) when (ex is TimeoutException or HttpRequestException)
        {
            submitResult = new SubmitRefundResult(
                Success: false,
                ProviderReference: null,
                Status: "NeedsAttention",
                IsUncertain: true,
                ErrorCode: "GatewayTimeout",
                ErrorMessage: ex.Message
            );
        }

        // BƯỚC 3: Xử lý kết quả trả về từ cổng
        if (submitResult.Success && string.Equals(submitResult.Status, "Succeeded", StringComparison.OrdinalIgnoreCase))
        {
            refund.MarkSucceeded(submitResult.ProviderReference, now, command.RequestedByUserId);

            var prevSucceeded = await db.Refunds
                .Where(r => r.PaymentAttemptId == attempt.Id && r.Status == RefundStatus.Succeeded && r.Id != refund.Id)
                .SumAsync(r => r.AmountVnd, ct);

            var totalSucceeded = prevSucceeded + refund.AmountVnd;
            var prevPaymentStatus = order.OrderPaymentStatus;
            var isFullyRefunded = totalSucceeded >= attempt.AmountVnd;
            order.OrderPaymentStatus = isFullyRefunded ? "Refunded" : "PartiallyRefunded";

            var history = OrderStatusHistory.Create(
                orderId: order.Id,
                axis: "Payment",
                fromStatus: prevPaymentStatus,
                toStatus: order.OrderPaymentStatus,
                actorUserId: command.RequestedByUserId,
                source: "Staff",
                reason: $"Hoàn tiền thành công: {refund.AmountVnd:N0} VNĐ",
                operationKey: $"refund:success:{refund.Id}",
                createdAtUtc: now
            );
            db.OrderStatusHistories.Add(history);

            var approvedCancellation = order.CancellationRequests
                .FirstOrDefault(c => c.RefundId == refund.Id);
            if (approvedCancellation != null)
            {
                approvedCancellation.Complete(now);
            }

            var approvedDefect = order.DefectReports
                .FirstOrDefault(d => d.RefundId == refund.Id);
            if (approvedDefect != null)
            {
                approvedDefect.Resolve(command.RequestedByUserId, approvedDefect.ResolutionNote ?? $"Đã hoàn tiền {refund.AmountVnd:N0} VNĐ", now);
            }

            var auditLog = AuditLog.Create(
                actorUserId: command.RequestedByUserId,
                action: "RefundSucceeded",
                aggregateType: "Refund",
                aggregateId: refund.Id,
                traceId: Guid.NewGuid().ToString("N"),
                redactedChangesJson: JsonSerializer.Serialize(new { fromPaymentStatus = prevPaymentStatus, toPaymentStatus = order.OrderPaymentStatus, amountVnd = refund.AmountVnd }, JsonOptions),
                reason: $"Hoàn tiền thành công: {refund.AmountVnd:N0} VNĐ",
                utcNow: now
            );
            db.AuditLogs.Add(auditLog);

            var outbox = OutboxMessage.Create(
                eventType: "OrderRefunded",
                aggregateId: order.Id,
                deduplicationKey: $"order:refunded:{refund.Id}",
                payloadJson: JsonSerializer.Serialize(new
                {
                    refundId = refund.Id,
                    orderId = order.Id,
                    orderCode = order.OrderCode,
                    amountVnd = refund.AmountVnd,
                    status = order.OrderPaymentStatus,
                    providerReference = refund.ProviderReference
                }, JsonOptions),
                utcNow: now
            );
            db.OutboxMessages.Add(outbox);
        }
        else if (submitResult.IsUncertain || string.Equals(submitResult.Status, "NeedsAttention", StringComparison.OrdinalIgnoreCase))
        {
            refund.MarkNeedsAttention(submitResult.ErrorMessage ?? "Cổng thanh toán phản hồi không xác định hoặc timeout");
            order.OrderPaymentStatus = "NeedsAttention";

            var history = OrderStatusHistory.Create(
                orderId: order.Id,
                axis: "Payment",
                fromStatus: "RefundPending",
                toStatus: "NeedsAttention",
                actorUserId: command.RequestedByUserId,
                source: "System",
                reason: "Lệnh hoàn tiền đang chờ đối soát do gateway timeout/uncertain",
                operationKey: $"refund:attention:{refund.Id}",
                createdAtUtc: now
            );
            db.OrderStatusHistories.Add(history);

            var auditLog = AuditLog.Create(
                actorUserId: command.RequestedByUserId,
                action: "RefundNeedsAttention",
                aggregateType: "Refund",
                aggregateId: refund.Id,
                traceId: Guid.NewGuid().ToString("N"),
                redactedChangesJson: JsonSerializer.Serialize(new { fromStatus = "Submitted", toStatus = "NeedsAttention", error = submitResult.ErrorMessage }, JsonOptions),
                reason: submitResult.ErrorMessage ?? "Lệnh hoàn tiền chuyển sang NeedsAttention",
                utcNow: now
            );
            db.AuditLogs.Add(auditLog);

            var outbox = OutboxMessage.Create(
                eventType: "RefundNeedsAttention",
                aggregateId: order.Id,
                deduplicationKey: $"refund:needs_attention:{refund.Id}",
                payloadJson: JsonSerializer.Serialize(new
                {
                    refundId = refund.Id,
                    orderId = order.Id,
                    amountVnd = refund.AmountVnd,
                    error = submitResult.ErrorMessage
                }, JsonOptions),
                utcNow: now
            );
            db.OutboxMessages.Add(outbox);
        }
        else
        {
            refund.MarkFailed(submitResult.ErrorMessage ?? "Cổng thanh toán từ chối lệnh hoàn tiền");

            var auditLog = AuditLog.Create(
                actorUserId: command.RequestedByUserId,
                action: "RefundFailed",
                aggregateType: "Refund",
                aggregateId: refund.Id,
                traceId: Guid.NewGuid().ToString("N"),
                redactedChangesJson: JsonSerializer.Serialize(new { fromStatus = "Submitted", toStatus = "Failed", error = submitResult.ErrorMessage }, JsonOptions),
                reason: submitResult.ErrorMessage ?? "Cổng từ chối hoàn tiền",
                utcNow: now
            );
            db.AuditLogs.Add(auditLog);
        }

        await db.SaveChangesAsync(ct);

        return Result<CreateRefundResult>.Success(new CreateRefundResult(MapToDto(refund), IsReplay: false));
    }

    private static RefundDto MapToDto(Refund r) => new(
        r.Id,
        r.PaymentAttemptId,
        r.RequestedByUserId,
        r.ApprovedByUserId,
        r.OperationKey,
        r.ProviderReference,
        r.AmountVnd,
        r.Status.ToString(),
        r.Reason,
        r.CreatedAtUtc,
        r.CompletedAtUtc
    );
}
