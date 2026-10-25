using MediatR;
using Microsoft.EntityFrameworkCore;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Application.Common.Models;
using TheBoys.Application.Notifications.DTOs;

namespace TheBoys.Application.Notifications.Queries;

public sealed record GetNotificationsQuery(
    Guid UserId,
    bool UnreadOnly = false
) : IRequest<Result<List<NotificationDto>>>;

public sealed class GetNotificationsQueryHandler(
    IApplicationDbContext db
) : IRequestHandler<GetNotificationsQuery, Result<List<NotificationDto>>>
{
    public async Task<Result<List<NotificationDto>>> Handle(GetNotificationsQuery query, CancellationToken ct)
    {
        var dbQuery = db.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == query.UserId);

        if (query.UnreadOnly)
        {
            dbQuery = dbQuery.Where(n => n.ReadAtUtc == null);
        }

        var items = await dbQuery
            .OrderByDescending(n => n.CreatedAtUtc)
            .Select(n => new NotificationDto(
                n.Id,
                n.UserId,
                n.OutboxMessageId,
                n.Channel.ToString(),
                n.Title,
                n.Body,
                n.DeliveredAtUtc,
                n.ReadAtUtc,
                n.CreatedAtUtc
            ))
            .ToListAsync(ct);

        return Result<List<NotificationDto>>.Success(items);
    }
}

