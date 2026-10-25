using System.Data;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Application.Common.Models;
using TheBoys.Application.Quotes;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;

namespace TheBoys.Application.Orders.Commands.CheckoutQuote;

public sealed class CheckoutQuoteCommandHandler(
    IApplicationDbContext db,
    IClock clock) : IRequestHandler<CheckoutQuoteCommand, Result<CheckoutQuoteResult>>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task<Result<CheckoutQuoteResult>> Handle(CheckoutQuoteCommand request, CancellationToken ct)
    {
        // 1. Check idempotency trước ngoài transaction
        var existingOrder = await db.Orders
            .Include(o => o.Items)
            .Include(o => o.StatusHistories)
            .Include(o => o.PricingPackage)
            .AsNoTracking()
            .SingleOrDefaultAsync(o => o.UserId == request.UserId && o.ClientRequestId == request.ClientRequestId, ct);

        if (existingOrder != null)
        {
            if (existingOrder.RequestFingerprint == request.RequestFingerprint)
            {
                return Result<CheckoutQuoteResult>.Success(new(existingOrder, IsReplay: true));
            }
            return Result<CheckoutQuoteResult>.Failure(Error.Conflict("IdempotencyMismatch", "Idempotency-Key đã dùng cho đơn hàng khác."));
        }

        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);

        // 2. Tải Quote và kiểm tra tính toàn vẹn
        var quote = await db.Quotes
            .Include(q => q.PricingPackage)
            .Include(q => q.Approval)
            .Include(q => q.Review)
            .Include(q => q.Version).ThenInclude(v => v.Assets)
            .Include(q => q.Version).ThenInclude(v => v.Project)
            .SingleOrDefaultAsync(q => q.Id == request.QuoteId, ct);

        if (quote == null)
            return Result<CheckoutQuoteResult>.Failure(Error.NotFound("QuoteNotFound", "Không tìm thấy báo giá."));

        if (quote.UserId != request.UserId)
            return Result<CheckoutQuoteResult>.Failure(Error.Unauthorized("NotOwner", "Không thể checkout báo giá của tài khoản khác."));

        var now = clock.UtcNow;
        if (quote.IsExpired(now))
            return Result<CheckoutQuoteResult>.Failure(Error.Conflict("QuoteExpired", "Báo giá đã hết hạn. Vui lòng tạo báo giá mới."));

        var quoteAlreadyUsed = await db.Orders.AnyAsync(o => o.QuoteId == quote.Id, ct);
        if (quoteAlreadyUsed)
            return Result<CheckoutQuoteResult>.Failure(Error.Conflict("QuoteAlreadyCheckedOut", "Báo giá này đã được tạo đơn hàng trước đó."));

        // Kiểm tra Approval và Review tham chiếu đúng VersionId & ConfigurationHash
        if (quote.Approval.VersionId != quote.VersionId ||
            quote.Review.VersionId != quote.VersionId ||
            !quote.Approval.ConfigurationHash.SequenceEqual(quote.ConfigurationHash) ||
            !quote.Review.ConfigurationHash.SequenceEqual(quote.ConfigurationHash) ||
            quote.Review.Decision != "Approved")
        {
            return Result<CheckoutQuoteResult>.Failure(Error.Conflict("InvalidQuoteState", "Dữ liệu kiểm duyệt hoặc phê duyệt của báo giá không hợp lệ."));
        }

        // Kiểm tra ModelVersion và Asset
        var version = quote.Version;
        var hasValidAsset = version.Assets.Any(a => a.PurgedAtUtc == null && a.Sha256 != null && a.Sha256.Length > 0);
        if (!hasValidAsset)
            return Result<CheckoutQuoteResult>.Failure(Error.Conflict("AssetCorruptedOrPurged", "Dữ liệu 3D của mô hình không hợp lệ hoặc đã bị dọn dẹp."));

        // Phân giải AddressJson
        var address = JsonSerializer.Deserialize<ShippingAddressDto>(quote.AddressJson, JsonOptions) ?? new ShippingAddressDto();

        // Trích xuất snapshot metadata
        decimal heightMm = version.HeightMm;
        string? engravingText = null;
        try
        {
            using var snapDoc = JsonDocument.Parse(quote.SnapshotJson);
            if (snapDoc.RootElement.TryGetProperty("heightMm", out var hElem) && hElem.TryGetDecimal(out var hVal))
            {
                heightMm = hVal;
            }
            if (snapDoc.RootElement.TryGetProperty("engravingText", out var engElem))
            {
                engravingText = engElem.GetString();
            }
        }
        catch
        {
            // Fallback
        }

        var orderCode = $"TB-{now:yyyyMMdd}-{Guid.NewGuid():N}";

        var order = new Order
        {
            OrderCode = orderCode,
            UserId = request.UserId,
            QuoteId = quote.Id,
            ClientRequestId = request.ClientRequestId,
            RequestFingerprint = request.RequestFingerprint,
            PricingPackageId = quote.PricingPackageId,
            Status = OrderStatus.PendingPayment,
            FulfillmentStatus = "PendingPayment",
            OrderPaymentStatus = "Unpaid",
            Currency = "VND",
            Quantity = quote.Quantity,
            UnitPriceSnapshot = quote.UnitPriceVnd,
            SubTotalVnd = decimal.Round(quote.UnitPriceVnd * quote.Quantity, 0, MidpointRounding.AwayFromZero),
            ShippingFeeRaw = quote.ShippingFeeVnd,
            FinalShippingFee = quote.ShippingFeeVnd,
            TotalAmount = quote.TotalVnd,
            PartnerCostSnapshot = quote.PricingPackage.PartnerBaseCostVnd,
            AiCostBudgetSnapshot = 0,
            DefectBudgetSnapshot = decimal.Round(quote.PricingPackage.PartnerBaseCostVnd * 0.05m, 2, MidpointRounding.AwayFromZero),
            ShippingSubsidySnapshot = 0,
            PaymentGatewayFeeSnapshot = 0,
            PrintHeightMm = (int)Math.Round(heightMm),
            WidthMmSnapshot = version.WidthMm,
            DepthMmSnapshot = version.DepthMm,
            HeightMmSnapshot = heightMm,
            MaterialSnapshot = quote.PricingPackage.Material,
            ReceiverName = address.ReceiverName.Trim(),
            ReceiverPhone = address.ReceiverPhone.Trim(),
            StreetAddress = address.StreetAddress.Trim(),
            District = address.District.Trim(),
            City = address.City.Trim(),
            CustomerNote = address.CustomerNote,
            AddressSnapshotJson = quote.AddressJson,
            PolicyVersion = quote.Approval.PolicyVersion,
            CreatedAtUtc = now
        };

        var orderItem = OrderItem.Create(
            order.Id,
            lineNumber: 1,
            versionId: quote.VersionId,
            configurationHash: quote.ConfigurationHash,
            assetHash: version.GeometryHash,
            quantity: quote.Quantity,
            unitPriceVnd: quote.UnitPriceVnd,
            materialSnapshot: quote.PricingPackage.Material,
            heightMm: heightMm,
            engravingText: engravingText,
            partnerUnitBudgetVnd: quote.PricingPackage.PartnerBaseCostVnd
        );
        order.Items.Add(orderItem);

        var fulfillmentHistory = OrderStatusHistory.Create(
            order.Id,
            axis: "Fulfillment",
            fromStatus: null,
            toStatus: "PendingPayment",
            actorUserId: request.UserId,
            source: "Checkout",
            reason: "Đơn hàng khởi tạo từ báo giá hợp lệ.",
            operationKey: $"init-fulfillment-{order.Id:N}",
            createdAtUtc: now
        );
        order.StatusHistories.Add(fulfillmentHistory);

        var paymentHistory = OrderStatusHistory.Create(
            order.Id,
            axis: "Payment",
            fromStatus: null,
            toStatus: "Unpaid",
            actorUserId: request.UserId,
            source: "Checkout",
            reason: "Chờ khách hàng thanh toán.",
            operationKey: $"init-payment-{order.Id:N}",
            createdAtUtc: now
        );
        order.StatusHistories.Add(paymentHistory);

        db.Orders.Add(order);

        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(ct);

            // Kiểm tra xem lỗi do QuoteId trùng (concurrency) hay do ClientRequestId trùng (idempotency race)
            var duplicateQuote = await db.Orders
                .AsNoTracking()
                .AnyAsync(o => o.QuoteId == quote.Id, ct);

            if (duplicateQuote)
            {
                return Result<CheckoutQuoteResult>.Failure(Error.Conflict("QuoteAlreadyCheckedOut", "Báo giá này đã được tạo đơn hàng trước đó."));
            }

            var duplicateIdempotency = await db.Orders
                .Include(o => o.Items)
                .Include(o => o.StatusHistories)
                .Include(o => o.PricingPackage)
                .AsNoTracking()
                .SingleOrDefaultAsync(o => o.UserId == request.UserId && o.ClientRequestId == request.ClientRequestId, ct);

            if (duplicateIdempotency != null)
            {
                if (duplicateIdempotency.RequestFingerprint == request.RequestFingerprint)
                {
                    return Result<CheckoutQuoteResult>.Success(new(duplicateIdempotency, IsReplay: true));
                }
                return Result<CheckoutQuoteResult>.Failure(Error.Conflict("IdempotencyMismatch", "Idempotency-Key đã dùng cho đơn hàng khác."));
            }

            throw;
        }

        return Result<CheckoutQuoteResult>.Success(new(order, IsReplay: false));
    }
}

