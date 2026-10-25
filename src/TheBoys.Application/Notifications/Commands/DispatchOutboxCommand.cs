using MediatR;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Application.Common.Models;

namespace TheBoys.Application.Notifications.Commands;

public sealed record DispatchOutboxCommand(
    int BatchSize = 50
) : IRequest<Result<OutboxDispatchBatchResult>>;

public sealed class DispatchOutboxCommandHandler(
    IOutboxDispatcher dispatcher
) : IRequestHandler<DispatchOutboxCommand, Result<OutboxDispatchBatchResult>>
{
    public async Task<Result<OutboxDispatchBatchResult>> Handle(DispatchOutboxCommand command, CancellationToken ct)
    {
        var result = await dispatcher.DispatchPendingAsync(command.BatchSize, ct);
        return Result<OutboxDispatchBatchResult>.Success(result);
    }
}

