using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TheBoys.Application.Orders.Commands.CancelOrder;
using TheBoys.Application.Orders.Commands.CreateOrder;
using TheBoys.Application.Orders.Commands.UpdateOrderStatus;
using TheBoys.Application.Orders.Queries;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;
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
public sealed class OrdersController(ISender mediator) : ApiControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateOrderRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? requestId, CancellationToken ct)
    {
        if (!Guid.TryParse(requestId, out var guid)) return BadRequest(new { message = "Idempotency-Key phải là UUID." });
        var key = guid.ToString("N");
        var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request)));

        var command = new CreateOrderCommand(
            UserId, request.GenerationTaskId, request.PricingPackageId, request.Quantity,
            request.ExpectedTotalVnd, request.ReceiverName, request.ReceiverPhone,
            request.StreetAddress, request.District, request.City, request.CustomerNote,
            key, fingerprint
        );

        var result = await mediator.Send(command, ct);
        if (result.IsFailure) return ProblemResult(result.Error);

        var (order, isReplay) = result.Value;
        return isReplay
            ? Ok(View(order))
            : CreatedAtAction(nameof(Get), new { id = order.Id }, View(order));
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery, Range(1, 100000)] int page = 1, CancellationToken ct = default)
    {
        var orders = await mediator.Send(new GetOrdersQuery(UserId, IsManagement, page), ct);
        return Ok(orders.Select(View));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetOrderByIdQuery(id, UserId, IsManagement), ct);
        return result.IsFailure ? ProblemResult(result.Error) : Ok(View(result.Value));
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new CancelOrderCommand(id, UserId, IsManagement), ct);
        return result.IsFailure ? ProblemResult(result.Error) : Ok(View(result.Value));
    }

    [HttpPatch("{id:guid}/status"), Authorize(Roles = Roles.Management)]
    public async Task<IActionResult> UpdateStatus(Guid id, UpdateOrderStatusRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new UpdateOrderStatusCommand(id, UserId, request.Status, request.Note, request.TrackingNumber), ct);
        return result.IsFailure ? ProblemResult(result.Error) : Ok(View(result.Value));
    }

    private static object View(Order o) => new
    {
        o.Id, o.OrderCode, o.UserId, o.GenerationTaskId, o.PricingPackageId, o.Status, fulfillmentStatus = o.FulfillmentStatus, orderPaymentStatus = o.OrderPaymentStatus, o.Quantity,
        unitPriceVnd = o.UnitPriceSnapshot, subtotalVnd = o.SubTotalVnd,
        shippingFeeVnd = o.FinalShippingFee, totalVnd = o.TotalAmount, material = o.MaterialSnapshot,
        dimensions = new { widthMm = o.WidthMmSnapshot, depthMm = o.DepthMmSnapshot, heightMm = o.HeightMmSnapshot },
        o.ReceiverName, o.ReceiverPhone, o.StreetAddress, o.District, o.City, o.CustomerNote,
        o.CreatedAtUtc, o.PrintingStartedAtUtc, o.PackagedAtUtc, o.ShippedAtUtc, o.DeliveredAtUtc, o.CancelledAtUtc,
        o.TrackingNumber, o.StatusNote, paymentMode = "demo_offline_no_online_charge"
    };
}
