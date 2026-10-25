using MediatR;
using Microsoft.EntityFrameworkCore;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Application.Common.Models;
using TheBoys.Application.Notifications.DTOs;

namespace TheBoys.Application.Notifications.Commands;

public sealed record MarkNotificationReadCommand(
    Guid NotificationId,
    Guid UserId,
    bool IsManagement = false
) : IRequest<Result<NotificationDto>>;

public sealed class MarkNotificationReadCommandHandler(
    IApplicationDbContext db,
    IClock clock
) : IRequestHandler<MarkNotificationReadCommand, Result<NotificationDto>>
{
    public async Task<Result<NotificationDto>> Handle(MarkNotificationReadCommand command, CancellationToken ct)
    {
        var notif = await db.Notifications
            .SingleOrDefaultAsync(n => n.Id == command.NotificationId, ct);

        if (notif == null)
        {
            return Result<NotificationDto>.Failure(Error.NotFound("NotificationNotFound", "Không tìm thấy thông báo."));
        }

        if (!command.IsManagement && notif.UserId != command.UserId)
        {
            return Result<NotificationDto>.Failure(Error.Unauthorized("Forbidden", "Bạn không có quyền truy cập thông báo này."));
        }

        if (notif.ReadAtUtc == null)
        {
            notif.MarkRead(clock.UtcNow);
            await db.SaveChangesAsync(ct);
        }

        return Result<NotificationDto>.Success(new NotificationDto(
            notif.Id,
            notif.UserId,
            notif.OutboxMessageId,
            notif.Channel.ToString(),
            notif.Title,
            notif.Body,
            notif.DeliveredAtUtc,
            notif.ReadAtUtc,
            notif.CreatedAtUtc
        ));
    }
}

