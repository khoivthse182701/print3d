using MediatR;
using Microsoft.EntityFrameworkCore;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Application.Common.Models;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;

namespace TheBoys.Application.Orders.Commands.CancelOrder;

public sealed record CancelOrderCommand(
    Guid OrderId,
    Guid UserId,
    bool IsManagement
) : IRequest<Result<Order>>;

public sealed class CancelOrderCommandHandler(IApplicationDbContext db) : IRequestHandler<CancelOrderCommand, Result<Order>>
{
    public async Task<Result<Order>> Handle(CancelOrderCommand request, CancellationToken ct)
    {
        var order = await db.Orders.SingleOrDefaultAsync(x => x.Id == request.OrderId, ct);
        if (order == null || (!request.IsManagement && order.UserId != request.UserId))
            return Result<Order>.Failure(Error.NotFound("OrderNotFound"));

        if (order.Status != OrderStatus.Draft)
            return Result<Order>.Failure(Error.Conflict("CannotCancel", "Chỉ hủy trực tiếp khi đơn chưa bắt đầu in."));

        order.Status = OrderStatus.Cancelled;
        order.CancelledAtUtc = DateTime.UtcNow;
        order.StatusNote = $"Hủy bởi {request.UserId}";

        await db.SaveChangesAsync(ct);
        return Result<Order>.Success(order);
    }
}
