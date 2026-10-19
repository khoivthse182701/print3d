using MediatR;
using Microsoft.EntityFrameworkCore;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Application.Common.Models;
using TheBoys.Domain.Entities;

namespace TheBoys.Application.Orders.Queries;

public sealed record GetOrdersQuery(
    Guid UserId,
    bool IsManagement,
    int Page = 1
) : IRequest<IReadOnlyList<Order>>;

public sealed class GetOrdersQueryHandler(IApplicationDbContext db) : IRequestHandler<GetOrdersQuery, IReadOnlyList<Order>>
{
    public async Task<IReadOnlyList<Order>> Handle(GetOrdersQuery request, CancellationToken ct)
    {
        var query = db.Orders.AsNoTracking();
        if (!request.IsManagement) query = query.Where(x => x.UserId == request.UserId);
        return await query.OrderByDescending(x => x.CreatedAtUtc)
            .Skip((request.Page - 1) * 20)
            .Take(20)
            .ToListAsync(ct);
    }
}

public sealed record GetOrderByIdQuery(
    Guid Id,
    Guid UserId,
    bool IsManagement
) : IRequest<Result<Order>>;

public sealed class GetOrderByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetOrderByIdQuery, Result<Order>>
{
    public async Task<Result<Order>> Handle(GetOrderByIdQuery request, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.Id, ct);
        if (order == null || (!request.IsManagement && order.UserId != request.UserId))
            return Result<Order>.Failure(Error.NotFound("OrderNotFound"));
        return Result<Order>.Success(order);
    }
}
