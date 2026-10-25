using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TheBoys.Application.Common.Interfaces;

namespace TheBoys.Infrastructure.Services;

public sealed class OutboxWorkerHostedService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<OutboxWorkerHostedService> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabled = configuration.GetValue<bool?>("OutboxSettings:Enabled") ?? true;
        if (!enabled)
        {
            logger.LogInformation("OutboxWorkerHostedService đã bị tắt qua cấu hình OutboxSettings:Enabled.");
            return;
        }

        var intervalSeconds = configuration.GetValue<int?>("OutboxSettings:IntervalSeconds") ?? 5;
        logger.LogInformation("OutboxWorkerHostedService khởi động với chu kỳ {Interval}s.", intervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<IOutboxDispatcher>();
                var result = await dispatcher.DispatchPendingAsync(batchSize: 50, stoppingToken);

                if (result.ProcessedCount > 0 || result.FailedCount > 0 || result.DeadLetteredCount > 0)
                {
                    logger.LogInformation(
                        "Outbox dispatch hoàn tất batch: Processed={Processed}, Failed={Failed}, DeadLettered={DeadLettered}",
                        result.ProcessedCount, result.FailedCount, result.DeadLetteredCount);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Lỗi xảy ra trong vòng lặp OutboxWorkerHostedService.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}

