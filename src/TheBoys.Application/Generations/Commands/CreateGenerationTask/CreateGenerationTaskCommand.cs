using MediatR;
using Microsoft.EntityFrameworkCore;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Application.Common.Models;
using TheBoys.Application.Printing;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;

namespace TheBoys.Application.Generations.Commands.CreateGenerationTask;

public sealed record CreateGenerationTaskResult(GenerationTask Task, bool IsReplay);

public sealed record CreateGenerationTaskCommand(
    Guid UserId,
    byte[] ImageData,
    string MimeType,
    int HeightMm,
    bool ShouldTexture,
    string? ModelName,
    string ClientRequestId,
    string RequestFingerprint
) : IRequest<Result<CreateGenerationTaskResult>>;

public sealed class CreateGenerationTaskCommandHandler(
    IApplicationDbContext db,
    IModelGenerationProvider provider,
    IModelStorage storage) : IRequestHandler<CreateGenerationTaskCommand, Result<CreateGenerationTaskResult>>
{
    public async Task<Result<CreateGenerationTaskResult>> Handle(CreateGenerationTaskCommand request, CancellationToken ct)
    {
        var previous = await db.GenerationTasks.AsNoTracking()
            .SingleOrDefaultAsync(x => x.UserId == request.UserId && x.ClientRequestId == request.ClientRequestId, ct);
        if (previous != null)
        {
            if (previous.RequestFingerprint == request.RequestFingerprint)
                return Result<CreateGenerationTaskResult>.Success(new(previous, IsReplay: true));
            return Result<CreateGenerationTaskResult>.Failure(Error.Conflict("IdempotencyMismatch", "Idempotency-Key đã dùng cho nội dung khác."));
        }

        if (!provider.IsConfigured)
            return Result<CreateGenerationTaskResult>.Failure(Error.ServiceUnavailable("ProviderNotConfigured", $"Chưa cấu hình API key cho {provider.Name} trên server."));

        var user = await db.Users.SingleAsync(x => x.Id == request.UserId, ct);
        if (!user.HasAvailableCredit())
            return Result<CreateGenerationTaskResult>.Failure(Error.PaymentRequired("NoCredit", "Bạn đã hết lượt tạo model."));

        var task = new GenerationTask
        {
            UserId = request.UserId,
            TaskType = TaskType.ImageTo3D,
            RequestedHeightMm = request.HeightMm,
            ShouldTexture = request.ShouldTexture,
            ModelName = request.ModelName?.Trim(),
            ClientRequestId = request.ClientRequestId,
            RequestFingerprint = request.RequestFingerprint,
            AiModel = provider.Model,
            Provider = provider.Name,
            ChargedPurchasedCredit = user.DeductCredit(),
            NextPollAtUtc = DateTime.UtcNow
        };

        task.InputImageUrl = await storage.SaveImageAsync(task.Id, request.ImageData, request.MimeType, ct);
        db.GenerationTasks.Add(task);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            try { System.IO.File.Delete(storage.ResolvePath(task.InputImageUrl)); } catch { }
            throw;
        }

        return Result<CreateGenerationTaskResult>.Success(new(task, IsReplay: false));
    }
}
