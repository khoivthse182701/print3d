using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TheBoys.Application.Generations.Commands.CreateGenerationTask;
using TheBoys.Application.Generations.Commands.RecoverGenerationTask;
using TheBoys.Application.Generations.Commands.ReviewGenerationTask;
using TheBoys.Application.Generations.Queries;
using TheBoys.Application.Printing;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;
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
public sealed class GenerationsController(ISender mediator, IModelStorage storage) : ApiControllerBase
{
    [HttpPost("image"), EnableRateLimiting("generation"), Consumes("multipart/form-data")]
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

        var command = new CreateGenerationTaskCommand(UserId, data, mime, request.HeightMm, request.ShouldTexture, request.ModelName, key, fingerprint);
        var result = await mediator.Send(command, ct);

        if (result.IsFailure) return ProblemResult(result.Error);

        var (task, isReplay) = result.Value;
        return isReplay
            ? Ok(View(task))
            : AcceptedAtAction(nameof(Get), new { id = task.Id }, View(task));
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery, Range(1, 100000)] int page = 1, CancellationToken ct = default)
    {
        var tasks = await mediator.Send(new GetGenerationTasksQuery(UserId, IsManagement, page), ct);
        return Ok(tasks.Select(View));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetGenerationTaskByIdQuery(id, UserId, IsManagement), ct);
        return result.IsFailure ? ProblemResult(result.Error) : Ok(View(result.Value));
    }

    [HttpGet("{id:guid}/quote")]
    public async Task<IActionResult> Quote(Guid id, [FromQuery] Guid packageId, [FromQuery] int quantity = 1, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetPrintQuoteQuery(id, packageId, quantity, UserId, IsManagement), ct);
        return result.IsFailure ? ProblemResult(result.Error) : Ok(result.Value);
    }

    [HttpGet("{id:guid}/files/{kind}")]
    public async Task<IActionResult> Download(Guid id, string kind, CancellationToken ct)
    {
        var result = await mediator.Send(new GetGenerationTaskByIdQuery(id, UserId, IsManagement), ct);
        if (result.IsFailure) return NotFound();
        var task = result.Value;

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
        var result = await mediator.Send(new ReviewGenerationTaskCommand(id, UserId, request.Approved, request.Note), ct);
        return result.IsFailure ? ProblemResult(result.Error) : Ok(View(result.Value));
    }

    [HttpPost("{id:guid}/recover"), Authorize(Roles = Roles.Management)]
    public async Task<IActionResult> Recover(Guid id, RecoverGenerationRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new RecoverGenerationTaskCommand(id, request.ProviderTaskId), ct);
        return result.IsFailure ? ProblemResult(result.Error) : Ok(View(result.Value));
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
