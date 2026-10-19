using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TheBoys.Application.Printing;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;
using TheBoys.Infrastructure.Persistence;
using TheBoys.WebApi.Security;

namespace TheBoys.WebApi.Controllers;

public sealed class GenerateImageRequest
{
    [Required] public IFormFile Image { get; set; } = default!;
    [Range(100, 120)] public int HeightMm { get; set; } = 100;
    public bool ShouldTexture { get; set; }
    [MaxLength(200)] public string? ModelName { get; set; }
}
public sealed class PrintReviewRequest
{
    public bool Approved { get; set; }
    [Required, StringLength(1000, MinimumLength = 3)] public string Note { get; set; } = "";
}
public sealed class RecoverGenerationRequest
{
    [MaxLength(150)] public string? ProviderTaskId { get; set; }
}

[ApiController, Route("api/generations"), Authorize]
public sealed class GenerationsController(ApplicationDbContext db, IModelGenerationProvider provider, IModelStorage storage,
    PrintPricingService pricing, IOptions<PrintPricingOptions> pricingOptions) : ApiControllerBase
{
    [HttpPost("image"), EnableRateLimiting("generation")]
    [RequestSizeLimit(17 * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 17 * 1024 * 1024)]
    public async Task<IActionResult> Create([FromForm] GenerateImageRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? requestId, CancellationToken ct)
    {
        if (!Guid.TryParse(requestId, out var requestGuid)) return BadRequest(new { message = "Idempotency-Key phải là UUID, dùng lại key khi gửi lại cùng yêu cầu." });
        if (request.Image.Length is < 12 or > 15 * 1024 * 1024) return BadRequest(new { message = "Ảnh cần có dung lượng từ 12 bytes đến 15 MB." });
        await using var buffer = new MemoryStream();
        await request.Image.CopyToAsync(buffer, ct);
        var data = buffer.ToArray();
        var mime = DetectImage(data);
        if (mime == null) return BadRequest(new { message = "Chỉ nhận nội dung PNG, JPEG hoặc WEBP hợp lệ." });
        var fingerprint = Convert.ToHexString(SHA256.HashData(data.Concat(Encoding.UTF8.GetBytes($"|{request.HeightMm}|{request.ShouldTexture}|{request.ModelName}")).ToArray()));
        var key = requestGuid.ToString("N");
        var previous = await db.GenerationTasks.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == UserId && x.ClientRequestId == key, ct);
        if (previous != null)
            return previous.RequestFingerprint == fingerprint ? Ok(View(previous)) : Conflict(new { message = "Idempotency-Key đã dùng cho nội dung khác." });
        if (!provider.IsConfigured) return StatusCode(503, new { message = "Chưa cấu hình AiProviders__Meshy__ApiKey trên server." });
        var user = await db.Users.SingleAsync(x => x.Id == UserId, ct);
        if (!user.HasAvailableCredit()) return StatusCode(402, new { message = "Bạn đã hết lượt tạo model." });
        var task = new GenerationTask
        {
            UserId = UserId, TaskType = TaskType.ImageTo3D, RequestedHeightMm = request.HeightMm,
            ShouldTexture = request.ShouldTexture, ModelName = request.ModelName?.Trim(),
            ClientRequestId = key, RequestFingerprint = fingerprint, AiModel = provider.Model,
            ChargedPurchasedCredit = user.DeductCredit(), NextPollAtUtc = DateTime.UtcNow
        };
        task.InputImageUrl = await storage.SaveImageAsync(task.Id, data, mime, ct);
        db.GenerationTasks.Add(task);
        try { await db.SaveChangesAsync(ct); }
        catch { System.IO.File.Delete(storage.ResolvePath(task.InputImageUrl)); throw; }
        return AcceptedAtAction(nameof(Get), new { id = task.Id }, View(task));
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery, Range(1, 100000)] int page = 1, CancellationToken ct = default)
    {
        var query = db.GenerationTasks.AsNoTracking();
        if (!IsManagement) query = query.Where(x => x.UserId == UserId);
        var tasks = await query.OrderByDescending(x => x.CreatedAtUtc).Skip((page - 1) * 20).Take(20).ToListAsync(ct);
        return Ok(tasks.Select(View));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var task = await db.GenerationTasks.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return task == null || !CanAccess(task.UserId) ? NotFound() : Ok(View(task));
    }

    [HttpGet("{id:guid}/quote")]
    public async Task<IActionResult> Quote(Guid id, [FromQuery] Guid packageId, [FromQuery] int quantity = 1, CancellationToken ct = default)
    {
        var task = await db.GenerationTasks.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (task == null || !CanAccess(task.UserId)) return NotFound();
        var package = await db.PricingPackages.AsNoTracking().SingleOrDefaultAsync(x => x.Id == packageId, ct);
        if (package == null) return NotFound(new { message = "Không tìm thấy gói in." });
        try { return Ok(pricing.Quote(task, package, quantity, pricingOptions.Value)); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpGet("{id:guid}/files/{kind}")]
    public async Task<IActionResult> Download(Guid id, string kind, CancellationToken ct)
    {
        var task = await db.GenerationTasks.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (task == null || !CanAccess(task.UserId)) return NotFound();
        // STL is available after dimensional verification; staff approval is still required to print an order.
        var key = kind switch
        {
            "image" => task.InputImageUrl,
            "glb" => task.StoragePathGlb,
            "stl" when task.Status == GenerationStatus.Succeeded => task.StoragePathStl,
            _ => null
        };
        if (key == null || !System.IO.File.Exists(storage.ResolvePath(key))) return NotFound();
        var mime = Path.GetExtension(key) switch { ".glb" => "model/gltf-binary", ".stl" => "model/stl", ".jpg" => "image/jpeg", ".png" => "image/png", ".webp" => "image/webp", _ => "application/octet-stream" };
        Response.Headers.CacheControl = "private, no-store";
        return PhysicalFile(storage.ResolvePath(key), mime, $"{id}{Path.GetExtension(key)}", enableRangeProcessing: true);
    }

    [HttpPost("{id:guid}/review"), Authorize(Roles = Roles.Management)]
    public async Task<IActionResult> Review(Guid id, PrintReviewRequest request, CancellationToken ct)
    {
        var task = await db.GenerationTasks.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (task == null) return NotFound();
        if (task.Status != GenerationStatus.Succeeded) return Conflict(new { message = "Model chưa hoàn tất." });
        if (request.Approved && (!task.IsWatertight || task.PrintabilityStatus is not ("healthy" or "warning") || task.StoragePathStl == null))
            return Conflict(new { message = "Model chưa đạt kiểm tra mesh/độ dày. Cần sửa file hoặc tạo mẫu khác trước khi duyệt." });
        if (string.IsNullOrWhiteSpace(request.Note)) return BadRequest();
        task.IsApprovedForPrinting = request.Approved;
        task.ReviewStatus = request.Approved ? PrintReviewStatus.Approved : PrintReviewStatus.Rejected;
        task.ReviewedByUserId = UserId; task.ReviewedAtUtc = DateTime.UtcNow; task.ReviewNote = request.Note.Trim();
        await db.SaveChangesAsync(ct);
        return Ok(View(task));
    }

    [HttpPost("{id:guid}/recover"), Authorize(Roles = Roles.Management)]
    public async Task<IActionResult> Recover(Guid id, RecoverGenerationRequest request, CancellationToken ct)
    {
        var task = await db.GenerationTasks.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (task == null) return NotFound();
        if (task.Status != GenerationStatus.NeedsAttention) return Conflict(new { message = "Chỉ đối soát task đang cần can thiệp." });
        var providerId = string.IsNullOrWhiteSpace(request.ProviderTaskId) ? task.ProviderId() : request.ProviderTaskId;
        if (string.IsNullOrWhiteSpace(providerId)) return BadRequest(new { message = "Cần task ID của đúng bước hiện tại từ Meshy Developer Platform." });
        try { await provider.GetAsync(task.Stage, providerId, ct); }
        catch (ProviderException ex) { return StatusCode(502, new { message = ex.Message }); }
        task.SetProviderId(providerId); task.SubmissionPending = false; task.ErrorMessage = null;
        task.Status = task.Stage == GenerationStage.Generate ? GenerationStatus.Processing : GenerationStatus.MeshOptimizing;
        task.NextPollAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(View(task));
    }

    private object View(GenerationTask t) => new
    {
        t.Id, t.UserId, t.ModelName, t.Status, t.Stage, t.Progress, t.RequestedHeightMm, t.ShouldTexture, t.AiModel,
        t.ErrorMessage, t.CreatedAtUtc, t.CompletedAtUtc, t.DurationSeconds,
        dimensions = new { widthMm = t.BoundingBoxX_Mm, depthMm = t.BoundingBoxY_Mm, heightMm = t.BoundingBoxZ_Mm },
        modelVolumeCm3 = t.EstimatedVolumeCm3, t.PrintabilityStatus, t.IsWatertight,
        printability = t.PrintabilityJson == null ? (JsonElement?)null : JsonSerializer.Deserialize<JsonElement>(t.PrintabilityJson),
        t.IsApprovedForPrinting, t.ReviewStatus, t.ReviewNote, t.ReviewedAtUtc,
        imageUrl = $"/api/generations/{t.Id}/files/image",
        glbUrl = t.StoragePathGlb == null ? null : $"/api/generations/{t.Id}/files/glb",
        stlUrl = t.Status != GenerationStatus.Succeeded || t.StoragePathStl == null ? null : $"/api/generations/{t.Id}/files/stl",
        providerTaskIds = !IsManagement ? null : new { generation = t.ProviderTaskId, resize = t.ResizeTaskId, convert = t.ConvertTaskId, analysis = t.AnalysisTaskId }
    };

    public static string? DetectImage(byte[] bytes)
    {
        if (bytes.Length < 12) return null;
        if (bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return "image/png";
        if (bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255) return "image/jpeg";
        if (Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF" && Encoding.ASCII.GetString(bytes, 8, 4) == "WEBP") return "image/webp";
        return null;
    }
}
