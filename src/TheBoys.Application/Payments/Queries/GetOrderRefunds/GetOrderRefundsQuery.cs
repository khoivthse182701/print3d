using MediatR;
using Microsoft.EntityFrameworkCore;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Application.Common.Models;
using TheBoys.Application.Payments.DTOs;

namespace TheBoys.Application.Payments.Queries.GetOrderRefunds;

public sealed record GetOrderRefundsQuery(
    Guid OrderId,
    Guid UserId,
    bool IsManagement
) : IRequest<Result<IReadOnlyList<RefundDto>>>;

public sealed class GetOrderRefundsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetOrderRefundsQuery, Result<IReadOnlyList<RefundDto>>>
{
    public async Task<Result<IReadOnlyList<RefundDto>>> Handle(GetOrderRefundsQuery query, CancellationToken ct)
    {
        var order = await db.Orders
            .AsNoTracking()
            .SingleOrDefaultAsync(o => o.Id == query.OrderId, ct);

        if (order == null)
            return Result<IReadOnlyList<RefundDto>>.Failure(Error.NotFound("OrderNotFound", "Không tìm thấy đơn hàng."));

        if (!query.IsManagement && order.UserId != query.UserId)
            return Result<IReadOnlyList<RefundDto>>.Failure(Error.Unauthorized("Forbidden", "Bạn không có quyền xem thông tin đơn hàng này."));

        var refunds = await db.Refunds
            .AsNoTracking()
            .Where(r => r.PaymentAttempt.OrderId == query.OrderId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .Select(r => new RefundDto(
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
            ))
            .ToListAsync(ct);

        return Result<IReadOnlyList<RefundDto>>.Success(refunds);
    }
}

