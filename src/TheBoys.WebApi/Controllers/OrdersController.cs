using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TheBoys.Application.Printing;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;
using TheBoys.Infrastructure.Persistence;
using TheBoys.WebApi.Security;

namespace TheBoys.WebApi.Controllers;

public sealed class CreateOrderRequest
{
    public Guid GenerationTaskId { get; set; }
    public Guid PricingPackageId { get; set; }
    [Range(1, 10)] public int Quantity { get; set; } = 1;
    [Range(typeof(decimal), "1", "1000000000")] public decimal ExpectedTotalVnd { get; set; }
    [Required, MaxLength(150)] public string ReceiverName { get; set; } = "";
    [Required, MaxLength(20)] public string ReceiverPhone { get; set; } = "";
    [Required, MaxLength(300)] public string StreetAddress { get; set; } = "";
    [Required, MaxLength(100)] public string District { get; set; } = "";
    [Required, MaxLength(100)] public string City { get; set; } = "Thành phố Hồ Chí Minh";
    [MaxLength(500)] public string? CustomerNote { get; set; }
}
public sealed class UpdateOrderStatusRequest
{
    public OrderStatus Status { get; set; }
    [Required, StringLength(500, MinimumLength = 3)] public string Note { get; set; } = "";
    [MaxLength(100)] public string? TrackingNumber { get; set; }
}

[ApiController, Route("api/orders"), Authorize]
public sealed class OrdersController(ApplicationDbContext db, PrintPricingService pricing,
    IOptions<PrintPricingOptions> options) : ApiControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateOrderRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? requestId, CancellationToken ct)
    {
        if (!Guid.TryParse(requestId, out var guid)) return BadRequest(new { message = "Idempotency-Key phải là UUID." });
        var key = guid.ToString("N");
        var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request)));
        var previous = await db.Orders.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == UserId && x.ClientRequestId == key, ct);
        if (previous != null) return previous.RequestFingerprint == fingerprint ? Ok(View(previous)) : Conflict(new { message = "Idempotency-Key đã dùng cho đơn khác." });
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var task = await db.GenerationTasks.SingleOrDefaultAsync(x => x.Id == request.GenerationTaskId && x.UserId == UserId, ct);
        if (task == null) return NotFound();
        if (task.ReviewStatus == PrintReviewStatus.Rejected) return Conflict(new { message = "Mẫu đã bị từ chối in. Cần mẫu khác hoặc được Staff duyệt lại." });
        var package = await db.PricingPackages.SingleOrDefaultAsync(x => x.Id == request.PricingPackageId, ct);
        if (package == null) return NotFound();
        PrintQuote quote;
        try { quote = pricing.Quote(task, package, request.Quantity, options.Value); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        if (request.ExpectedTotalVnd != quote.TotalVnd) return Conflict(new { message = "Giá đã thay đổi; xác nhận báo giá mới.", quote });
        var order = new Order
        {
            OrderCode = $"TB-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}",
            UserId = UserId, GenerationTaskId = task.Id, PricingPackageId = package.Id,
            ClientRequestId = key, RequestFingerprint = fingerprint, Status = OrderStatus.Draft,
            Quantity = quote.Quantity, UnitPriceSnapshot = quote.UnitPriceVnd, SubTotalVnd = quote.SubtotalVnd,
            ShippingFeeRaw = quote.ShippingFeeVnd, FinalShippingFee = quote.ShippingFeeVnd, TotalAmount = quote.TotalVnd,
            PartnerCostSnapshot = package.PartnerBaseCostVnd, AiCostBudgetSnapshot = 10000,
            DefectBudgetSnapshot = package.PartnerBaseCostVnd * 0.05m, ShippingSubsidySnapshot = 0, PaymentGatewayFeeSnapshot = 0,
            PrintHeightMm = task.RequestedHeightMm, WidthMmSnapshot = quote.WidthMm,
            DepthMmSnapshot = quote.DepthMm, HeightMmSnapshot = quote.HeightMm, MaterialSnapshot = package.Material,
            ReceiverName = request.ReceiverName.Trim(), ReceiverPhone = request.ReceiverPhone.Trim(),
            StreetAddress = request.StreetAddress.Trim(), District = request.District.Trim(), City = request.City.Trim(),
            CustomerNote = request.CustomerNote
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = order.Id }, View(order));
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery, Range(1, 100000)] int page = 1, CancellationToken ct = default)
    {
        var query = db.Orders.AsNoTracking();
        if (!IsManagement) query = query.Where(x => x.UserId == UserId);
        var orders = await query.OrderByDescending(x => x.CreatedAtUtc).Skip((page - 1) * 20).Take(20).ToListAsync(ct);
        return Ok(orders.Select(View));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return order == null || !CanAccess(order.UserId) ? NotFound() : Ok(View(order));
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var order = await db.Orders.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (order == null || !CanAccess(order.UserId)) return NotFound();
        if (order.Status != OrderStatus.Draft) return Conflict(new { message = "Chỉ hủy trực tiếp khi đơn chưa bắt đầu in." });
        order.Status = OrderStatus.Cancelled; order.CancelledAtUtc = DateTime.UtcNow;
        order.StatusNote = $"Hủy bởi {UserId}";
        await db.SaveChangesAsync(ct);
        return Ok(View(order));
    }

    [HttpPatch("{id:guid}/status"), Authorize(Roles = Roles.Management)]
    public async Task<IActionResult> UpdateStatus(Guid id, UpdateOrderStatusRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var order = await db.Orders.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (order == null) return NotFound();
        var permitted = (order.Status, request.Status) switch
        {
            (OrderStatus.Draft, OrderStatus.Printing or OrderStatus.Cancelled) => true,
            (OrderStatus.Printing, OrderStatus.Packaged) => true,
            (OrderStatus.Packaged, OrderStatus.Shipping) => true,
            (OrderStatus.Shipping, OrderStatus.Delivered) => true,
            _ => false
        };
        if (!permitted) return Conflict(new { message = "Không được bỏ qua hoặc quay ngược bước xử lý đơn." });
        if (request.Status == OrderStatus.Printing)
        {
            var task = await db.GenerationTasks.SingleAsync(x => x.Id == order.GenerationTaskId, ct);
            if (!task.IsApprovedForPrinting || task.Status != GenerationStatus.Succeeded)
                return Conflict(new { message = "Staff phải duyệt model trước khi bắt đầu in." });
            order.PrintingStartedAtUtc = DateTime.UtcNow;
        }
        if (request.Status == OrderStatus.Packaged) order.PackagedAtUtc = DateTime.UtcNow;
        if (request.Status == OrderStatus.Shipping) { order.ShippedAtUtc = DateTime.UtcNow; order.TrackingNumber = request.TrackingNumber; }
        if (request.Status == OrderStatus.Delivered) order.DeliveredAtUtc = DateTime.UtcNow;
        if (request.Status == OrderStatus.Cancelled) order.CancelledAtUtc = DateTime.UtcNow;
        order.Status = request.Status; order.StatusNote = $"{UserId}: {request.Note}";
        if (order.StatusNote.Length > 500) order.StatusNote = order.StatusNote[..500];
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return Ok(View(order));
    }

    private static object View(Order o) => new
    {
        o.Id, o.OrderCode, o.UserId, o.GenerationTaskId, o.PricingPackageId, o.Status, o.Quantity,
        unitPriceVnd = o.UnitPriceSnapshot, subtotalVnd = o.SubTotalVnd,
        shippingFeeVnd = o.FinalShippingFee, totalVnd = o.TotalAmount, material = o.MaterialSnapshot,
        dimensions = new { widthMm = o.WidthMmSnapshot, depthMm = o.DepthMmSnapshot, heightMm = o.HeightMmSnapshot },
        o.ReceiverName, o.ReceiverPhone, o.StreetAddress, o.District, o.City, o.CustomerNote,
        o.CreatedAtUtc, o.PrintingStartedAtUtc, o.PackagedAtUtc, o.ShippedAtUtc, o.DeliveredAtUtc, o.CancelledAtUtc,
        o.TrackingNumber, o.StatusNote, paymentMode = "demo_offline_no_online_charge"
    };
}
