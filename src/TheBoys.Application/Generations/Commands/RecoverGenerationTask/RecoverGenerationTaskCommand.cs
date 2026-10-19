using MediatR;
using Microsoft.EntityFrameworkCore;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Application.Common.Models;
using TheBoys.Application.Printing;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;

namespace TheBoys.Application.Generations.Commands.RecoverGenerationTask;

public sealed record RecoverGenerationTaskCommand(
    Guid TaskId,
    string? ProviderTaskId
) : IRequest<Result<GenerationTask>>;

public sealed class RecoverGenerationTaskCommandHandler(
    IApplicationDbContext db,
    IModelGenerationProvider provider) : IRequestHandler<RecoverGenerationTaskCommand, Result<GenerationTask>>
{
    public async Task<Result<GenerationTask>> Handle(RecoverGenerationTaskCommand request, CancellationToken ct)
    {
        var task = await db.GenerationTasks.SingleOrDefaultAsync(x => x.Id == request.TaskId, ct);
        if (task == null) return Result<GenerationTask>.Failure(Error.NotFound("TaskNotFound", "Không tìm thấy tác vụ."));
        if (task.Status != GenerationStatus.NeedsAttention)
            return Result<GenerationTask>.Failure(Error.Conflict("InvalidStatus", "Chỉ đối soát task đang cần can thiệp."));

        var providerId = string.IsNullOrWhiteSpace(request.ProviderTaskId) ? task.ProviderId() : request.ProviderTaskId;
        if (string.IsNullOrWhiteSpace(providerId))
            return Result<GenerationTask>.Failure(Error.Validation("MissingProviderId", $"Cần task ID của đúng bước hiện tại từ {provider.Name}."));

        try
        {
            await provider.GetAsync(task.Stage, providerId, ct);
        }
        catch (ProviderException ex)
        {
            return Result<GenerationTask>.Failure(Error.BadGateway("ProviderError", ex.Message));
        }

        task.SetProviderId(providerId);
        task.SubmissionPending = false;
        task.ErrorMessage = null;
        task.Status = task.Stage == GenerationStage.Generate ? GenerationStatus.Processing : GenerationStatus.MeshOptimizing;
        task.NextPollAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return Result<GenerationTask>.Success(task);
    }
}
