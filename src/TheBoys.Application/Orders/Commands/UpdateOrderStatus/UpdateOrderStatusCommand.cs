using System.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Application.Common.Models;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;

namespace TheBoys.Application.Orders.Commands.UpdateOrderStatus;

public sealed record UpdateOrderStatusCommand(
    Guid OrderId,
    Guid StaffUserId,
    OrderStatus Status,
    string Note,
    string? TrackingNumber
) : IRequest<Result<Order>>;

public sealed class UpdateOrderStatusCommandHandler(IApplicationDbContext db)
    : IRequestHandler<UpdateOrderStatusCommand, Result<Order>>
{
    public async Task<Result<Order>> Handle(UpdateOrderStatusCommand request, CancellationToken ct)
    {
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);

        var order = await db.Orders.SingleOrDefaultAsync(x => x.Id == request.OrderId, ct);
        if (order == null) return Result<Order>.Failure(Error.NotFound("OrderNotFound"));

        var permitted = (order.Status, request.Status) switch
        {
            (OrderStatus.Draft, OrderStatus.Printing or OrderStatus.Cancelled) => true,
            (OrderStatus.Printing, OrderStatus.Packaged) => true,
            (OrderStatus.Packaged, OrderStatus.Shipping) => true,
            (OrderStatus.Shipping, OrderStatus.Delivered) => true,
            _ => false
        };

        if (!permitted)
            return Result<Order>.Failure(Error.Conflict("InvalidTransition", "Không được bỏ qua hoặc quay ngược bước xử lý đơn."));

        if (request.Status == OrderStatus.Printing)
        {
            var task = await db.GenerationTasks.SingleAsync(x => x.Id == order.GenerationTaskId, ct);
            if (!task.IsApprovedForPrinting || task.Status != GenerationStatus.Succeeded)
                return Result<Order>.Failure(Error.Conflict("TaskNotApproved", "Staff phải duyệt model trước khi bắt đầu in."));
            order.PrintingStartedAtUtc = DateTime.UtcNow;
        }

        if (request.Status == OrderStatus.Packaged) order.PackagedAtUtc = DateTime.UtcNow;
        if (request.Status == OrderStatus.Shipping) { order.ShippedAtUtc = DateTime.UtcNow; order.TrackingNumber = request.TrackingNumber; }
        if (request.Status == OrderStatus.Delivered) order.DeliveredAtUtc = DateTime.UtcNow;
        if (request.Status == OrderStatus.Cancelled) order.CancelledAtUtc = DateTime.UtcNow;

        order.Status = request.Status;
        order.StatusNote = $"{request.StaffUserId}: {request.Note}";
        if (order.StatusNote.Length > 500) order.StatusNote = order.StatusNote[..500];

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return Result<Order>.Success(order);
    }
}
