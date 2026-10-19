using System.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Application.Common.Models;
using TheBoys.Application.Printing;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;

namespace TheBoys.Application.Orders.Commands.CreateOrder;

public sealed record CreateOrderResult(Order Order, bool IsReplay);

public sealed record CreateOrderCommand(
    Guid UserId,
    Guid GenerationTaskId,
    Guid PricingPackageId,
    int Quantity,
    decimal ExpectedTotalVnd,
    string ReceiverName,
    string ReceiverPhone,
    string StreetAddress,
    string District,
    string City,
    string? CustomerNote,
    string ClientRequestId,
    string RequestFingerprint
) : IRequest<Result<CreateOrderResult>>;

public sealed class CreateOrderCommandHandler(
    IApplicationDbContext db,
    PrintPricingService pricing,
    IOptions<PrintPricingOptions> options) : IRequestHandler<CreateOrderCommand, Result<CreateOrderResult>>
{
    public async Task<Result<CreateOrderResult>> Handle(CreateOrderCommand request, CancellationToken ct)
    {
        var previous = await db.Orders.AsNoTracking()
            .SingleOrDefaultAsync(x => x.UserId == request.UserId && x.ClientRequestId == request.ClientRequestId, ct);
        if (previous != null)
        {
            if (previous.RequestFingerprint == request.RequestFingerprint)
                return Result<CreateOrderResult>.Success(new(previous, IsReplay: true));
            return Result<CreateOrderResult>.Failure(Error.Conflict("IdempotencyMismatch", "Idempotency-Key đã dùng cho đơn khác."));
        }

        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);

        var task = await db.GenerationTasks.SingleOrDefaultAsync(x => x.Id == request.GenerationTaskId && x.UserId == request.UserId, ct);
        if (task == null) return Result<CreateOrderResult>.Failure(Error.NotFound("TaskNotFound"));

        if (task.ReviewStatus == PrintReviewStatus.Rejected)
            return Result<CreateOrderResult>.Failure(Error.Conflict("TaskRejected", "Mẫu đã bị từ chối in. Cần mẫu khác hoặc được Staff duyệt lại."));

        var package = await db.PricingPackages.SingleOrDefaultAsync(x => x.Id == request.PricingPackageId, ct);
        if (package == null) return Result<CreateOrderResult>.Failure(Error.NotFound("PackageNotFound"));

        PrintQuote quote;
        try
        {
            quote = pricing.Quote(task, package, request.Quantity, options.Value);
        }
        catch (ArgumentException ex)
        {
            return Result<CreateOrderResult>.Failure(Error.Validation("InvalidQuote", ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Result<CreateOrderResult>.Failure(Error.Conflict("CannotQuote", ex.Message));
        }

        if (request.ExpectedTotalVnd != quote.TotalVnd)
        {
            return Result<CreateOrderResult>.Failure(Error.Conflict("PriceChanged", "Giá đã thay đổi; xác nhận báo giá mới.", quote));
        }

        var order = new Order
        {
            OrderCode = $"TB-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}",
            UserId = request.UserId,
            GenerationTaskId = task.Id,
            PricingPackageId = package.Id,
            ClientRequestId = request.ClientRequestId,
            RequestFingerprint = request.RequestFingerprint,
            Status = OrderStatus.Draft,
            Quantity = quote.Quantity,
            UnitPriceSnapshot = quote.UnitPriceVnd,
            SubTotalVnd = quote.SubtotalVnd,
            ShippingFeeRaw = quote.ShippingFeeVnd,
            FinalShippingFee = quote.ShippingFeeVnd,
            TotalAmount = quote.TotalVnd,
            PartnerCostSnapshot = package.PartnerBaseCostVnd,
            AiCostBudgetSnapshot = 10000,
            DefectBudgetSnapshot = package.PartnerBaseCostVnd * 0.05m,
            ShippingSubsidySnapshot = 0,
            PaymentGatewayFeeSnapshot = 0,
            PrintHeightMm = task.RequestedHeightMm,
            WidthMmSnapshot = quote.WidthMm,
            DepthMmSnapshot = quote.DepthMm,
            HeightMmSnapshot = quote.HeightMm,
            MaterialSnapshot = package.Material,
            ReceiverName = request.ReceiverName.Trim(),
            ReceiverPhone = request.ReceiverPhone.Trim(),
            StreetAddress = request.StreetAddress.Trim(),
            District = request.District.Trim(),
            City = request.City.Trim(),
            CustomerNote = request.CustomerNote
        };

        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return Result<CreateOrderResult>.Success(new(order, IsReplay: false));
    }
}
