namespace TheBoys.Application.Common.Interfaces;

public sealed record OutboxDispatchBatchResult(
    int ProcessedCount,
    int FailedCount,
    int DeadLetteredCount
);

public interface IOutboxDispatcher
{
    Task<OutboxDispatchBatchResult> DispatchPendingAsync(int batchSize = 50, CancellationToken ct = default);
}

