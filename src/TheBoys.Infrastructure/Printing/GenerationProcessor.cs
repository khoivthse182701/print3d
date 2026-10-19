using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TheBoys.Application.Printing;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;
using TheBoys.Infrastructure.Persistence;

namespace TheBoys.Infrastructure.Printing;

public sealed class GenerationProcessor(ApplicationDbContext db, IModelGenerationProvider provider,
    IModelStorage storage, ILogger<GenerationProcessor> logger)
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
            try
            {
                if (task.SubmissionPending)
                {
                    task.Status = GenerationStatus.NeedsAttention;
                    task.ErrorMessage = "Có yêu cầu Meshy chưa đối soát được. Staff nhập task ID từ Developer Platform để tiếp tục; không tự gửi lại.";
                }
                else if (task.ProviderId() is not { } providerId)
                {
                    task.SubmissionPending = true;
                    task.Status = task.Stage == GenerationStage.Generate ? GenerationStatus.Processing : GenerationStatus.MeshOptimizing;
                    task.UpdatedAtUtc = DateTime.UtcNow;
                    await db.SaveChangesAsync(ct); // Persist BEFORE the external side effect.
                    var newId = await provider.CreateAsync(task, ct);
                    task.SetProviderId(newId);
                    task.SubmissionPending = false;
                    task.ErrorMessage = null;
                    task.NextPollAtUtc = DateTime.UtcNow.AddSeconds(5);
                }
                else
                {
                    var result = await provider.GetAsync(task.Stage, providerId, ct);
                    task.NextPollAtUtc = DateTime.UtcNow.AddSeconds(result.RetryAfterSeconds);
                    if (result.Status is "FAILED" or "CANCELED")
                        await FailAsync(task, result.Error ?? "Meshy không hoàn tất task.", ct);
                    else if (result.Status == "SUCCEEDED")
                        await FinishStageAsync(task, result, ct);
                    else if (result.Status is "PENDING" or "IN_PROGRESS")
                    {
                        task.Progress = Math.Min(99, (int)task.Stage * 25 + Math.Clamp(result.Progress, 0, 100) / 4);
                        task.ErrorMessage = null;
                    }
                    else throw new ProviderException("Meshy trả trạng thái chưa hỗ trợ; cần kiểm tra task.");
                }
            }
            catch (ProviderException ex)
            {
                if (ex.Uncertain) { task.Status = GenerationStatus.NeedsAttention; task.ErrorMessage = ex.Message; }
                else if (ex.Transient) { task.ErrorMessage = ex.Message; task.NextPollAtUtc = DateTime.UtcNow.AddSeconds(30); }
                else if (task.SubmissionPending)
                {
                    task.SubmissionPending = false;
                    await FailAsync(task, ex.Message, CancellationToken.None);
                }
                else { task.Status = GenerationStatus.NeedsAttention; task.ErrorMessage = ex.Message; }
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException or InvalidOperationException or KeyNotFoundException or OperationCanceledException)
            {
                logger.LogWarning("Generation {TaskId}, stage {Stage}: {ErrorType}", task.Id, task.Stage, ex.GetType().Name);
                // Includes download errors: keep existing provider IDs and resume, never regenerate.
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

    private async Task FailAsync(GenerationTask task, string error, CancellationToken ct)
    {
        task.Status = GenerationStatus.Failed; task.ErrorMessage = error; task.CompletedAtUtc = DateTime.UtcNow;
        // Refund app quota once only when generation itself failed. Later stages already produced a model.
        if (task.Stage == GenerationStage.Generate && !task.CreditRefunded)
        {
            var user = await db.Users.SingleAsync(x => x.Id == task.UserId, ct);
            user.RefundCredit(task.ChargedPurchasedCredit); task.CreditRefunded = true;
        }
    }

    private async Task FinishStageAsync(GenerationTask task, ProviderTask result, CancellationToken ct)
    {
        switch (task.Stage)
        {
            case GenerationStage.Generate:
                task.Stage = GenerationStage.Resize;
                break;
            case GenerationStage.Resize:
                task.ResizedModelUrl = result.ModelUrls["glb"];
                task.StoragePathGlb = await storage.DownloadAsync(task.Id, "glb", task.ResizedModelUrl, ct);
                task.Stage = GenerationStage.Convert;
                break;
            case GenerationStage.Convert:
                task.StoragePathStl = await storage.DownloadAsync(task.Id, "stl", result.ModelUrls["stl"], ct);
                task.Stage = GenerationStage.Analyze;
                break;
            case GenerationStage.Analyze:
                ApplyAnalysis(task, result.Printability ?? throw new JsonException("Missing printability"));
                StlGeometry.NormalizeToMillimeters(storage.ResolvePath(task.StoragePathStl!), task.BoundingBoxX_Mm!.Value,
                    task.BoundingBoxY_Mm!.Value, task.BoundingBoxZ_Mm!.Value);
                task.Stage = GenerationStage.Complete; task.Status = GenerationStatus.Succeeded;
                task.CompletedAtUtc = DateTime.UtcNow;
                task.DurationSeconds = (int)(DateTime.UtcNow - task.CreatedAtUtc).TotalSeconds;
                break;
        }
        task.Progress = Math.Min(100, (int)task.Stage * 25);
        if (task.Stage != GenerationStage.Complete) task.Status = GenerationStatus.MeshOptimizing;
        task.ErrorMessage = null;
        task.NextPollAtUtc = DateTime.UtcNow.AddSeconds(5);
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
