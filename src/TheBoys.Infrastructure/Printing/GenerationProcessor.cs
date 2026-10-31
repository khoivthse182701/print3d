using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Application.Printing;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;
using TheBoys.Infrastructure.Persistence;

namespace TheBoys.Infrastructure.Printing;

public sealed class GenerationProcessor(
    ApplicationDbContext db,
    IModelProviderRegistry providerRegistry,
    IModelStorage storage,
    ICreditService creditService,
    ILogger<GenerationProcessor> logger,
    IHostEnvironment env)
{
    public async Task ProcessAsync(Guid id, CancellationToken ct)
    {
        // SQL session lock prevents two API/worker instances from submitting the same stage.
        await db.Database.OpenConnectionAsync(ct);
        var connection = db.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "DECLARE @result int; EXEC @result = sp_getapplock @Resource=@key, @LockMode='Exclusive', @LockOwner='Session', @LockTimeout=0; SELECT @result;";
        var p = command.CreateParameter(); p.ParameterName = "@key"; p.Value = $"generation:{id}"; command.Parameters.Add(p);
        var acquired = false;
        try
        {
            if (Convert.ToInt32(await command.ExecuteScalarAsync(ct)) < 0) return;
            acquired = true;
            var task = await db.GenerationTasks.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (task == null || task.Status is not (GenerationStatus.Queued or GenerationStatus.Processing or GenerationStatus.MeshOptimizing)) return;

            // AI-A06: Resolve pinned provider from job/task metadata. Never fallback silently in prod!
            IModelGenerationProvider provider;
            try
            {
                provider = providerRegistry.Resolve(task.Provider);
            }
            catch (ProviderException ex)
            {
                if (env.IsDevelopment() && ex.Message.Contains("chưa được cấu hình API key"))
                {
                    await CompleteWithDevelopmentFallbackAsync(task, ct);
                    return;
                }

                task.Status = GenerationStatus.NeedsAttention;
                task.ErrorMessage = $"Provider '{task.Provider}' không thể thực hiện tác vụ: {ex.Message}";
                task.UpdatedAtUtc = DateTime.UtcNow;

                var currentStage = await db.GenerationStages.FirstOrDefaultAsync(s => s.JobId == task.Id && s.IsActive, ct);
                if (currentStage != null)
                {
                    currentStage.Status = "NeedsAttention";
                    currentStage.ErrorMessage = task.ErrorMessage;
                }
                var currentJob = await db.GenerationJobs.FirstOrDefaultAsync(j => j.Id == task.Id, ct);
                if (currentJob != null)
                {
                    currentJob.Status = "NeedsAttention";
                    currentJob.ErrorMessage = task.ErrorMessage;
                }

                await db.SaveChangesAsync(CancellationToken.None);
                return;
            }

            try
            {
                if (task.SubmissionPending)
                {
                    task.Status = GenerationStatus.NeedsAttention;
                    task.ErrorMessage = $"Có yêu cầu {provider.Name} chưa đối soát được. Staff nhập task ID để tiếp tục; không tự gửi lại.";
                }
                else if (task.ProviderId() is not { } providerId)
                {
                    task.SubmissionPending = true;
                    task.Status = task.Stage == TheBoys.Domain.Enums.GenerationStage.Generate ? GenerationStatus.Processing : GenerationStatus.MeshOptimizing;
                    task.UpdatedAtUtc = DateTime.UtcNow;

                    var activeStage = await db.GenerationStages.FirstOrDefaultAsync(s => s.JobId == task.Id && s.IsActive, ct);
                    if (activeStage != null)
                    {
                        activeStage.Status = "Submitting";
                        activeStage.SubmissionPending = true;
                    }
                    var activeJob = await db.GenerationJobs.FirstOrDefaultAsync(j => j.Id == task.Id, ct);
                    if (activeJob != null)
                    {
                        activeJob.Status = "Processing";
                    }

                    await db.SaveChangesAsync(ct); // Persist BEFORE the external side effect.

                    var newId = await provider.CreateAsync(task, ct);
                    task.SetProviderId(newId);
                    task.SubmissionPending = false;
                    task.ErrorMessage = null;
                    task.NextPollAtUtc = DateTime.UtcNow.AddSeconds(5);

                    if (activeStage != null)
                    {
                        activeStage.ProviderTaskId = newId;
                        activeStage.SubmissionPending = false;
                        activeStage.Status = "Running";
                        activeStage.NextPollAtUtc = task.NextPollAtUtc;
                    }
                }
                else
                {
                    var result = await provider.GetAsync(task.Stage, providerId, ct);
                    task.NextPollAtUtc = DateTime.UtcNow.AddSeconds(result.RetryAfterSeconds);

                    var activeStage = await db.GenerationStages.FirstOrDefaultAsync(s => s.JobId == task.Id && s.IsActive, ct);
                    if (activeStage != null)
                    {
                        activeStage.NextPollAtUtc = task.NextPollAtUtc;
                        if (result.ProviderCredits.HasValue)
                            activeStage.ProviderCredits = result.ProviderCredits.Value;
                    }

                    if (result.Status is "FAILED" or "CANCELED")
                    {
                        var isTransientProviderError = !string.IsNullOrEmpty(result.Error) &&
                            (result.Error.Contains("internal service error", StringComparison.OrdinalIgnoreCase) ||
                             result.Error.Contains("temporar", StringComparison.OrdinalIgnoreCase) ||
                             result.Error.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                             result.Error.Contains("overload", StringComparison.OrdinalIgnoreCase));

                        if (task.Stage == TheBoys.Domain.Enums.GenerationStage.Generate && isTransientProviderError && task.RetryCount < 2)
                        {
                            task.RetryCount++;
                            task.ProviderTaskId = null;
                            task.Status = GenerationStatus.Queued;
                            task.Progress = 0;
                            task.NextPollAtUtc = DateTime.UtcNow.AddSeconds(5);
                            task.ErrorMessage = $"AI Provider tạm thời báo lỗi ({result.Error}). Hệ thống đang tự động thử lại (lần {task.RetryCount}/2)...";
                            logger.LogWarning("Task {Id} gặp lỗi transient từ provider ({Error}). Tự động thử lại lần {RetryCount}.", task.Id, result.Error, task.RetryCount);

                            if (activeStage != null)
                            {
                                activeStage.ProviderTaskId = null;
                                activeStage.Status = "Queued";
                                activeStage.Progress = 0;
                                activeStage.NextPollAtUtc = task.NextPollAtUtc;
                                activeStage.ErrorMessage = task.ErrorMessage;
                            }
                        }
                        else if (env.IsDevelopment() && provider is Hunyuan3DProvider or MeshyProvider or TripoProvider)
                        {
                            logger.LogWarning("Provider {Provider} returned FAILED ({Error}) in Development mode. Completing with sample 3D fallback.", provider.Name, result.Error);
                            await CompleteWithDevelopmentFallbackAsync(task, ct);
                            return;
                        }
                        else
                        {
                            await FailAsync(task, result.Error ?? $"{provider.Name} không hoàn tất task.", ct);
                        }
                    }
                    else if (result.Status == "SUCCEEDED")
                        await FinishStageAsync(task, provider, result, ct);
                    else if (result.Status is "PENDING" or "IN_PROGRESS")
                    {
                        task.Progress = Math.Min(99, (int)task.Stage * 25 + Math.Clamp(result.Progress, 0, 100) / 4);
                        task.ErrorMessage = null;
                        if (activeStage != null)
                        {
                            activeStage.Progress = result.Progress;
                            activeStage.Status = "Running";
                            activeStage.ErrorMessage = null;
                        }
                    }
                    else throw new ProviderException($"{provider.Name} trả trạng thái chưa hỗ trợ; cần kiểm tra task.");
                }
            }
            catch (ProviderException ex)
            {
                if (env.IsDevelopment() && provider is Hunyuan3DProvider or MeshyProvider or TripoProvider && (!provider.IsConfigured || ex.Message.Contains("RequestLimitExceeded") || ex.Message.Contains("chưa được cấu hình API key")))
                {
                    logger.LogWarning(ex, "Provider {Provider} unavailable ({Message}) in Development mode. Completing with sample 3D fallback.", provider.Name, ex.Message);
                    await CompleteWithDevelopmentFallbackAsync(task, ct);
                    return;
                }

                if (ex.Uncertain)
                {
                    task.Status = GenerationStatus.NeedsAttention;
                    task.ErrorMessage = ex.Message;
                }
                else if (ex.Transient)
                {
                    task.ErrorMessage = ex.Message;
                    task.NextPollAtUtc = DateTime.UtcNow.AddSeconds(30);
                }
                else if (task.SubmissionPending)
                {
                    task.SubmissionPending = false;
                    await FailAsync(task, ex.Message, CancellationToken.None);
                }
                else
                {
                    task.Status = GenerationStatus.NeedsAttention;
                    task.ErrorMessage = ex.Message;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException or InvalidOperationException or KeyNotFoundException or OperationCanceledException)
            {
                if (env.IsDevelopment() && ex is HttpRequestException && provider is Hunyuan3DProvider or MeshyProvider or TripoProvider)
                {
                    logger.LogWarning(ex, "Provider {Provider} network call failed in Development mode. Completing with sample 3D fallback.", provider.Name);
                    await CompleteWithDevelopmentFallbackAsync(task, ct);
                    return;
                }

                logger.LogWarning("Generation {TaskId}, stage {Stage}: {ErrorType}", task.Id, task.Stage, ex.GetType().Name);
                task.Status = GenerationStatus.NeedsAttention;
                task.ErrorMessage = "Cần kiểm tra kết quả/tải file của bước hiện tại. Staff có thể tiếp tục bằng task ID đã lưu.";
            }

            task.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(CancellationToken.None);
        }
        finally
        {
            if (acquired && connection.State == ConnectionState.Open)
            {
                await using var release = connection.CreateCommand();
                release.CommandText = "EXEC sp_releaseapplock @Resource=@key, @LockOwner='Session';";
                var key = release.CreateParameter(); key.ParameterName = "@key"; key.Value = $"generation:{id}"; release.Parameters.Add(key);
                await release.ExecuteNonQueryAsync(CancellationToken.None);
            }
            await db.Database.CloseConnectionAsync();
        }
    }

    private async Task CompleteWithDevelopmentFallbackAsync(GenerationTask task, CancellationToken ct)
    {
        logger.LogInformation("Generation {TaskId} running in Development mode: Generating fallback valid 3D model", task.Id);

        var targetHeight = task.RequestedHeightMm > 0 ? task.RequestedHeightMm : 100;
        var heightM = targetHeight / 1000f;
        var glbBytes = GlbToStlConverter.CreateCubeGlb(heightM * 0.75f, heightM, heightM * 0.45f);

        task.StoragePathGlb = storage.ModelKey(task.Id, "glb");
        var glbPath = storage.ResolvePath(task.StoragePathGlb);
        Directory.CreateDirectory(Path.GetDirectoryName(glbPath)!);
        await File.WriteAllBytesAsync(glbPath, glbBytes, ct);

        task.StoragePathStl = storage.ModelKey(task.Id, "stl");
        var stlPath = storage.ResolvePath(task.StoragePathStl);
        Directory.CreateDirectory(Path.GetDirectoryName(stlPath)!);
        GlbToStlConverter.ConvertGlbFileToBinaryStl(glbPath, stlPath);

        var analysis = StlGeometry.AnalyzePrintability(stlPath, targetHeight);
        ApplyAnalysis(task, analysis);
        StlGeometry.NormalizeToMillimeters(stlPath, task.BoundingBoxX_Mm!.Value, task.BoundingBoxY_Mm!.Value, task.BoundingBoxZ_Mm!.Value);

        task.Stage = TheBoys.Domain.Enums.GenerationStage.Complete;
        task.Status = GenerationStatus.Succeeded;
        task.Progress = 100;
        task.CompletedAtUtc = DateTime.UtcNow;
        task.DurationSeconds = Math.Max(1, (int)(DateTime.UtcNow - task.CreatedAtUtc).TotalSeconds);
        task.ErrorMessage = null;
        task.SubmissionPending = false;

        var stages = await db.GenerationStages.Where(s => s.JobId == task.Id && s.IsActive).ToListAsync(ct);
        foreach (var s in stages)
        {
            s.Status = "Succeeded";
            s.Progress = 100;
            s.CompletedAtUtc = DateTime.UtcNow;
            s.IsActive = false;
        }

        await PersistModelAssetsAsync(task, ct);
        await db.SaveChangesAsync(ct);
    }

    private async Task FailAsync(GenerationTask task, string error, CancellationToken ct)
    {
        task.Status = GenerationStatus.Failed;
        task.ErrorMessage = error;
        task.CompletedAtUtc = DateTime.UtcNow;

        var stage = await db.GenerationStages.FirstOrDefaultAsync(s => s.JobId == task.Id && s.IsActive, ct);
        if (stage != null)
        {
            stage.Status = "Failed";
            stage.ErrorMessage = error;
            stage.CompletedAtUtc = DateTime.UtcNow;
            stage.IsActive = false;
        }

        var job = await db.GenerationJobs.FirstOrDefaultAsync(j => j.Id == task.Id, ct);
        if (job != null)
        {
            job.Status = "Failed";
            job.ErrorMessage = error;
            job.CompletedAtUtc = DateTime.UtcNow;
        }

        // Refund app quota once only when generation itself failed. Later stages already produced a model.
        if (task.Stage == TheBoys.Domain.Enums.GenerationStage.Generate && !task.CreditRefunded)
        {
            var refundResult = await creditService.RefundForGenerationAsync(task.UserId, task.Id, $"generation:refund:{task.Id}", ct);
            if (refundResult.IsSuccess)
            {
                task.CreditRefunded = true;
            }
        }
    }

    private async Task FinishStageAsync(GenerationTask task, IModelGenerationProvider provider, ProviderTask result, CancellationToken ct)
    {
        // Close current active stage
        var activeStage = await db.GenerationStages.FirstOrDefaultAsync(s => s.JobId == task.Id && s.IsActive, ct);
        if (activeStage != null)
        {
            activeStage.Status = "Succeeded";
            activeStage.Progress = 100;
            activeStage.CompletedAtUtc = DateTime.UtcNow;
            activeStage.IsActive = false;
            if (result.ProviderCredits.HasValue)
                activeStage.ProviderCredits = result.ProviderCredits.Value;
        }

        TheBoys.Domain.Enums.GenerationStage nextStageEnum;
        switch (task.Stage)
        {
            case TheBoys.Domain.Enums.GenerationStage.Generate:
                if (provider.RequiresResizeStage)
                {
                    task.Stage = TheBoys.Domain.Enums.GenerationStage.Resize;
                    nextStageEnum = TheBoys.Domain.Enums.GenerationStage.Resize;
                }
                else
                {
                    task.ResizedModelUrl = result.ModelUrls["glb"];
                    task.StoragePathGlb = await storage.DownloadAsync(task.Id, "glb", task.ResizedModelUrl, ct);
                    task.Stage = TheBoys.Domain.Enums.GenerationStage.Convert;
                    nextStageEnum = TheBoys.Domain.Enums.GenerationStage.Convert;
                }
                break;

            case TheBoys.Domain.Enums.GenerationStage.Resize:
                task.ResizedModelUrl = result.ModelUrls["glb"];
                task.StoragePathGlb = await storage.DownloadAsync(task.Id, "glb", task.ResizedModelUrl, ct);
                task.Stage = TheBoys.Domain.Enums.GenerationStage.Convert;
                nextStageEnum = TheBoys.Domain.Enums.GenerationStage.Convert;
                break;

            case TheBoys.Domain.Enums.GenerationStage.Convert:
                task.StoragePathStl = await storage.DownloadAsync(task.Id, "stl", result.ModelUrls["stl"], ct);
                task.Stage = TheBoys.Domain.Enums.GenerationStage.Analyze;
                nextStageEnum = TheBoys.Domain.Enums.GenerationStage.Analyze;
                break;

            case TheBoys.Domain.Enums.GenerationStage.Analyze:
                ApplyAnalysis(task, result.Printability ?? throw new JsonException("Missing printability"));
                StlGeometry.NormalizeToMillimeters(storage.ResolvePath(task.StoragePathStl!), task.BoundingBoxX_Mm!.Value,
                    task.BoundingBoxY_Mm!.Value, task.BoundingBoxZ_Mm!.Value);
                task.Stage = TheBoys.Domain.Enums.GenerationStage.Complete;
                task.Status = GenerationStatus.Succeeded;
                task.CompletedAtUtc = DateTime.UtcNow;
                task.DurationSeconds = (int)(DateTime.UtcNow - task.CreatedAtUtc).TotalSeconds;
                nextStageEnum = TheBoys.Domain.Enums.GenerationStage.Complete;

                // MODEL-A05: Create ModelVersion and ModelAsset records
                await PersistModelAssetsAsync(task, ct);
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }

        // If advancing to a subsequent stage, create the next active GenerationStage entity
        if (nextStageEnum != TheBoys.Domain.Enums.GenerationStage.Complete)
        {
            var newStageEntity = new TheBoys.Domain.Entities.GenerationStage
            {
                Id = Guid.NewGuid(),
                JobId = task.Id,
                Stage = nextStageEnum.ToString(),
                AttemptNo = 1,
                Provider = provider.Name,
                ProviderAccountScope = "default",
                Status = "Queued",
                Progress = 0,
                IsActive = true,
                SubmissionPending = false,
                PollFailureCount = 0,
                NextPollAtUtc = DateTime.UtcNow.AddSeconds(5),
                CreatedAtUtc = DateTime.UtcNow
            };
            db.GenerationStages.Add(newStageEntity);
            task.Status = GenerationStatus.MeshOptimizing;
        }

        task.Progress = Math.Min(100, (int)task.Stage * 25);
        task.ErrorMessage = null;
        task.NextPollAtUtc = DateTime.UtcNow.AddSeconds(5);
    }

    private async Task PersistModelAssetsAsync(GenerationTask task, CancellationToken ct)
    {
        var job = await db.GenerationJobs.FirstOrDefaultAsync(j => j.Id == task.Id, ct);
        if (job == null) return;

        job.Status = "Succeeded";
        job.CompletedAtUtc = DateTime.UtcNow;

        var glbPath = storage.ResolvePath(task.StoragePathGlb!);
        var stlPath = storage.ResolvePath(task.StoragePathStl!);

        var glbBytes = await File.ReadAllBytesAsync(glbPath, ct);
        var stlBytes = await File.ReadAllBytesAsync(stlPath, ct);

        var glbSha256 = SHA256.HashData(glbBytes);
        var stlSha256 = SHA256.HashData(stlBytes);

        var existingVersionCount = await db.ModelVersions.CountAsync(v => v.ProjectId == job.ProjectId, ct);

        var version = new ModelVersion
        {
            Id = Guid.NewGuid(),
            ProjectId = job.ProjectId,
            SourceJobId = job.Id,
            VersionNo = existingVersionCount + 1,
            GeometryHash = stlSha256,
            WidthMm = task.BoundingBoxX_Mm ?? 0,
            DepthMm = task.BoundingBoxY_Mm ?? 0,
            HeightMm = task.BoundingBoxZ_Mm ?? task.RequestedHeightMm,
            VolumeCm3 = task.EstimatedVolumeCm3,
            IsWatertight = task.IsWatertight,
            PrintabilityStatus = task.PrintabilityStatus ?? "healthy",
            AnalysisCoverage = "GeometryAndThickness",
            AnalysisJson = task.PrintabilityJson,
            IsPublished = true,
            PublishedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.ModelVersions.Add(version);

        var glbAsset = new ModelAsset
        {
            Id = Guid.NewGuid(),
            VersionId = version.Id,
            Format = "GLB",
            Purpose = "Preview",
            StorageKey = task.StoragePathGlb!,
            Sha256 = glbSha256,
            ByteLength = glbBytes.LongLength,
            CoordinateUnit = "m",
            UpAxis = "Y",
            CreatedAtUtc = DateTime.UtcNow
        };
        db.ModelAssets.Add(glbAsset);

        var stlAsset = new ModelAsset
        {
            Id = Guid.NewGuid(),
            VersionId = version.Id,
            Format = "STL",
            Purpose = "Print",
            StorageKey = task.StoragePathStl!,
            Sha256 = stlSha256,
            ByteLength = stlBytes.LongLength,
            CoordinateUnit = "mm",
            UpAxis = "Z",
            CreatedAtUtc = DateTime.UtcNow
        };
        db.ModelAssets.Add(stlAsset);
    }

    public static void ApplyAnalysis(GenerationTask task, JsonElement analysis)
    {
        var dimensions = analysis.GetProperty("intended_print").GetProperty("dimensions_mm");
        if (dimensions.ValueKind != JsonValueKind.Array || dimensions.GetArrayLength() != 3) throw new JsonException("Missing dimensions");
        var width = dimensions[0].GetDecimal(); var depth = dimensions[1].GetDecimal(); var height = dimensions[2].GetDecimal();
        if (width <= 0 || depth <= 0 || height <= 0 || width > 10000 || depth > 10000 || Math.Abs(height - task.RequestedHeightMm) > 1)
            throw new InvalidOperationException("Kích thước trả về không khớp chiều cao yêu cầu.");
        // API dimensions are [width, depth, height]. Persist X=width, Y=depth, Z=height.
        task.BoundingBoxX_Mm = width; task.BoundingBoxY_Mm = depth; task.BoundingBoxZ_Mm = height;
        var metrics = analysis.GetProperty("metrics");
        task.IsWatertight = metrics.GetProperty("is_watertight").GetBoolean();
        var volumeM3 = metrics.GetProperty("volume").GetDecimal();
        task.EstimatedVolumeCm3 = task.IsWatertight && volumeM3 > 0 ? decimal.Round(volumeM3 * 1_000_000m, 4) : null;
        task.PrintabilityStatus = analysis.GetProperty("status").GetString();
        task.PrintabilityJson = analysis.GetRawText();
        // wall_thickness_mm is a threshold, NOT a measured minimum thickness.
        task.MinWallThicknessMm = null;
        task.IsApprovedForPrinting = false; task.ReviewStatus = PrintReviewStatus.Pending;
    }
}
