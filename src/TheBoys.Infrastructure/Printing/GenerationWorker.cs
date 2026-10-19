using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TheBoys.Application.Printing;
using TheBoys.Domain.Enums;
using TheBoys.Infrastructure.Persistence;

namespace TheBoys.Infrastructure.Printing;

public sealed class GenerationWorker(IServiceScopeFactory scopes, ILogger<GenerationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                List<Guid> ids;
                using (var scope = scopes.CreateScope())
                {
                    var provider = scope.ServiceProvider.GetRequiredService<IModelGenerationProvider>();
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    ids = provider.IsConfigured ? await db.GenerationTasks.AsNoTracking()
                        .Where(x => (x.Status == GenerationStatus.Queued || x.Status == GenerationStatus.Processing || x.Status == GenerationStatus.MeshOptimizing)
                            && (x.NextPollAtUtc == null || x.NextPollAtUtc <= DateTime.UtcNow))
                        .OrderBy(x => x.NextPollAtUtc).Take(10).Select(x => x.Id).ToListAsync(stoppingToken) : [];
                }
                foreach (var id in ids)
                {
                    using var scope = scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<GenerationProcessor>().ProcessAsync(id, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning("Generation worker will retry; error type: {Type}", ex.GetType().Name); }
            try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
