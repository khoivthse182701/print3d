using System.Security.Cryptography;
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
    string RequestFingerprint,
    string? PreferredProvider = null
) : IRequest<Result<CreateGenerationTaskResult>>;

public sealed class CreateGenerationTaskCommandHandler(
    IApplicationDbContext db,
    IModelProviderRegistry providerRegistry,
    IModelStorage storage,
    ICreditService creditService,
    IConsentService consentService,
    IImageValidationService imageValidationService,
    IContentModerationService contentModerationService,
    IClock clock) : IRequestHandler<CreateGenerationTaskCommand, Result<CreateGenerationTaskResult>>
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

        // AI-A06: Resolve provider for new job and validate capabilities
        IModelGenerationProvider provider;
        ProviderCapabilities capabilities;
        try
        {
            provider = providerRegistry.ResolveForNewJob(request.PreferredProvider, "ImageTo3D");
            capabilities = providerRegistry.GetCapabilities(provider.Name, provider.Model);
        }
        catch (ProviderException ex)
        {
            return Result<CreateGenerationTaskResult>.Failure(Error.ServiceUnavailable("ProviderUnavailable", ex.Message));
        }

        if (request.HeightMm < capabilities.MinHeightMm || request.HeightMm > capabilities.MaxHeightMm)
        {
            return Result<CreateGenerationTaskResult>.Failure(Error.Validation("InvalidHeight",
                $"Chiều cao mô hình phải từ {capabilities.MinHeightMm} mm đến {capabilities.MaxHeightMm} mm cho {provider.Name}."));
        }

        // Enforce user consent for AI image processing
        var hasConsent = await consentService.HasValidConsentAsync(request.UserId, "AiModelProcessing", "v1.0", ct);
        if (!hasConsent)
            return Result<CreateGenerationTaskResult>.Failure(Error.Validation("ConsentRequired", "Bạn cần cấp quyền đồng ý điều khoản xử lý hình ảnh AI trước khi tạo tác vụ."));

        // Validate actual image decode and pixel limits (max 40 MP, max 15 MiB)
        var validationResult = imageValidationService.ValidateImage(request.ImageData, request.MimeType);
        if (validationResult.IsFailure)
            return Result<CreateGenerationTaskResult>.Failure(validationResult.Error);

        // Anti-NSFW Guard: Prompt text and image content moderation
        if (!contentModerationService.IsContentSafe(request.ModelName, request.ImageData))
        {
            return Result<CreateGenerationTaskResult>.Failure(
                Error.Custom(422, "NSFW_CONTENT_BLOCKED", "Nội dung hình ảnh hoặc từ khóa vi phạm tiêu chuẩn kiểm duyệt (nhạy cảm, bạo lực hoặc 18+)."));
        }

        // Quick balance check before heavy storage operations
        var balance = await creditService.GetBalanceAsync(request.UserId, ct);
        if (balance.TotalBalance <= 0)
            return Result<CreateGenerationTaskResult>.Failure(Error.PaymentRequired("NoCredit", "Bạn đã hết lượt tạo model."));

        var taskId = Guid.NewGuid();
        var inputImageUrl = await storage.SaveImageAsync(taskId, request.ImageData, validationResult.Value.DetectedMimeType, ct);

        // MODEL-A05: Resolve or create default ModelProject
        var projectName = string.IsNullOrWhiteSpace(request.ModelName) ? "Default Project" : request.ModelName.Trim();
        var project = await db.ModelProjects
            .FirstOrDefaultAsync(x => x.UserId == request.UserId && x.Name == projectName && x.ArchivedAtUtc == null, ct);
        if (project == null)
        {
            project = new ModelProject
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                Name = projectName,
                CreatedAtUtc = clock.UtcNow
            };
            db.ModelProjects.Add(project);
        }

        // Parse client request id & fingerprint
        var clientRequestIdGuid = Guid.TryParse(request.ClientRequestId, out var parsedGuid) ? parsedGuid : Guid.NewGuid();
        byte[] rawFingerprintBytes;
        try { rawFingerprintBytes = Convert.FromHexString(request.RequestFingerprint); }
        catch { rawFingerprintBytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(request.RequestFingerprint)); }
        var inputHash = SHA256.HashData(request.ImageData);

        // MODEL-A05: Create pinned GenerationJob
        var job = new GenerationJob
        {
            Id = taskId,
            ProjectId = project.Id,
            UserId = request.UserId,
            Operation = "ImageTo3D",
            Provider = provider.Name,
            ProviderModel = provider.Model,
            ApiVersion = capabilities.ApiVersion,
            PolicyVersion = "v1.0",
            PricingVersion = capabilities.PricingVersion,
            AccountScope = capabilities.AccountScope,
            InputStorageKey = inputImageUrl,
            InputHash = inputHash,
            RequestedHeightMm = request.HeightMm,
            ShouldTexture = request.ShouldTexture,
            Status = "Queued",
            ClientRequestId = clientRequestIdGuid,
            RequestFingerprint = rawFingerprintBytes,
            CreatedAtUtc = clock.UtcNow
        };
        db.GenerationJobs.Add(job);

        // MODEL-A05: Create initial GenerationStage
        var stage = new TheBoys.Domain.Entities.GenerationStage
        {
            Id = Guid.NewGuid(),
            JobId = taskId,
            Stage = "Generate",
            AttemptNo = 1,
            Provider = provider.Name,
            ProviderAccountScope = capabilities.AccountScope,
            Status = "Queued",
            Progress = 0,
            IsActive = true,
            SubmissionPending = false,
            PollFailureCount = 0,
            NextPollAtUtc = clock.UtcNow,
            CreatedAtUtc = clock.UtcNow
        };
        db.GenerationStages.Add(stage);

        // Legacy compatibility projection
        var task = new GenerationTask
        {
            Id = taskId,
            UserId = request.UserId,
            TaskType = TaskType.ImageTo3D,
            RequestedHeightMm = request.HeightMm,
            ShouldTexture = request.ShouldTexture,
            ModelName = request.ModelName?.Trim(),
            ClientRequestId = request.ClientRequestId,
            RequestFingerprint = request.RequestFingerprint,
            AiModel = provider.Model,
            Provider = provider.Name,
            NextPollAtUtc = clock.UtcNow,
            InputImageUrl = inputImageUrl
        };
        db.GenerationTasks.Add(task);

        await db.SaveChangesAsync(ct);

        // Atomically debit credit via CreditService (satisfies FK GenerationTaskId -> GenerationTasks)
        var debitResult = await creditService.DebitForGenerationAsync(
            request.UserId,
            taskId,
            $"generation:debit:{taskId}",
            ct);

        if (debitResult.IsFailure)
        {
            try
            {
                var existingTask = await db.GenerationTasks.FirstOrDefaultAsync(x => x.Id == taskId, ct);
                if (existingTask != null) db.GenerationTasks.Remove(existingTask);

                var existingStage = await db.GenerationStages.FirstOrDefaultAsync(x => x.JobId == taskId, ct);
                if (existingStage != null) db.GenerationStages.Remove(existingStage);

                var existingJob = await db.GenerationJobs.FirstOrDefaultAsync(x => x.Id == taskId, ct);
                if (existingJob != null) db.GenerationJobs.Remove(existingJob);

                await db.SaveChangesAsync(ct);
            }
            catch { }
            try { System.IO.File.Delete(storage.ResolvePath(task.InputImageUrl)); } catch { }
            return Result<CreateGenerationTaskResult>.Failure(debitResult.Error);
        }

        task.ChargedPurchasedCredit = debitResult.Value.Bucket == CreditBucket.Purchased;
        await db.SaveChangesAsync(ct);

        return Result<CreateGenerationTaskResult>.Success(new(task, IsReplay: false));
    }
}
