using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Application.Common.Models;
using TheBoys.Domain.Entities;

namespace TheBoys.Application.Reviews.Commands.CreatePrintReview;

public sealed class CreatePrintReviewCommandHandler(
    IApplicationDbContext db,
    IConfigurationHasher hasher,
    IClock clock) : IRequestHandler<CreatePrintReviewCommand, Result<PrintReviewDto>>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task<Result<PrintReviewDto>> Handle(CreatePrintReviewCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Note))
            return Result<PrintReviewDto>.Failure(Error.Validation("InvalidNote", "Ghi chú review không được để trống."));

        if (request.Decision != "Approved" && request.Decision != "Rejected")
            return Result<PrintReviewDto>.Failure(Error.Validation("InvalidDecision", "Quyết định review phải là 'Approved' hoặc 'Rejected'."));

        var version = await db.ModelVersions
            .Include(v => v.Assets)
            .SingleOrDefaultAsync(v => v.Id == request.VersionId, ct);

        if (version == null)
            return Result<PrintReviewDto>.Failure(Error.NotFound("VersionNotFound", "Không tìm thấy phiên bản mô hình."));

        var (hash, _) = hasher.ComputeHash(request.Configuration);

        if (request.Checklist == null)
            return Result<PrintReviewDto>.Failure(Error.Validation("InvalidChecklist", "Checklist không được để trống."));

        if (request.Decision == "Approved")
        {
            if (!version.IsWatertight)
                return Result<PrintReviewDto>.Failure(Error.Conflict("MeshCheckFailed", "Mô hình không kín nước (watertight). Không thể duyệt in."));

            if (string.Equals(version.PrintabilityStatus, "failed", StringComparison.OrdinalIgnoreCase))
                return Result<PrintReviewDto>.Failure(Error.Conflict("PrintabilityCheckFailed", "Trạng thái in không đạt yêu cầu."));

            var hasValidPrintAsset = version.Assets.Any(a =>
                a.PurgedAtUtc == null &&
                a.Sha256 != null &&
                a.Sha256.Length > 0 &&
                (string.Equals(a.Format, "STL", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(a.Format, "GLB", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(a.Purpose, "Print", StringComparison.OrdinalIgnoreCase)));

            if (!hasValidPrintAsset)
                return Result<PrintReviewDto>.Failure(Error.Conflict("AssetNotFound", "Mô hình chưa có asset in hợp lệ với checksum Sha256 hoặc asset đã bị purge."));

            if (string.IsNullOrWhiteSpace(request.Checklist.Orientation))
                return Result<PrintReviewDto>.Failure(Error.Validation("InvalidOrientation", "Cần xác định hướng in (orientation)."));

            if (string.IsNullOrWhiteSpace(request.Checklist.Material))
                return Result<PrintReviewDto>.Failure(Error.Validation("InvalidMaterial", "Cần xác định vật liệu in."));

            if (request.Checklist.PrintHeightMm <= 0)
                return Result<PrintReviewDto>.Failure(Error.Validation("InvalidHeight", "Chiều cao in phải lớn hơn 0."));

            if (!request.Checklist.SupportConfirmation && string.IsNullOrWhiteSpace(request.Checklist.ExceptionReason))
                return Result<PrintReviewDto>.Failure(Error.Validation("SupportRequired", "Cần xác nhận support hoặc cung cấp lý do ngoại lệ."));

            if (!request.Checklist.BuildVolumeConfirmation && string.IsNullOrWhiteSpace(request.Checklist.ExceptionReason))
                return Result<PrintReviewDto>.Failure(Error.Validation("BuildVolumeExceeded", "Cần xác nhận kích thước build volume hoặc cung cấp lý do ngoại lệ."));
        }

        var checklistJson = JsonSerializer.Serialize(request.Checklist, JsonOptions);

        var review = PrintReview.Create(
            request.VersionId,
            hash,
            request.ReviewerUserId,
            request.Decision,
            checklistJson,
            request.EvidenceStorageKey,
            request.Note,
            clock.UtcNow
        );

        db.PrintReviews.Add(review);
        await db.SaveChangesAsync(ct);

        return Result<PrintReviewDto>.Success(new PrintReviewDto(
            review.Id,
            review.VersionId,
            Convert.ToHexString(review.ConfigurationHash).ToLowerInvariant(),
            review.ReviewerUserId,
            review.Decision,
            review.ChecklistJson,
            review.EvidenceStorageKey,
            review.Note,
            review.CreatedAtUtc
        ));
    }
}

