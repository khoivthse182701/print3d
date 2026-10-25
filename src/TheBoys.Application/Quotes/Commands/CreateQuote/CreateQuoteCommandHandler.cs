using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Application.Common.Models;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;

namespace TheBoys.Application.Quotes.Commands.CreateQuote;

public sealed class CreateQuoteCommandHandler(
    IApplicationDbContext db,
    IConfigurationHasher hasher,
    IOptions<QuoteOptions> options,
    IClock clock) : IRequestHandler<CreateQuoteCommand, Result<QuoteDto>>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task<Result<QuoteDto>> Handle(CreateQuoteCommand request, CancellationToken ct)
    {
        if (request.Quantity < 1 || request.Quantity > options.Value.MaxQuantity)
            return Result<QuoteDto>.Failure(Error.Validation("InvalidQuantity", $"Số lượng phải từ 1 đến {options.Value.MaxQuantity}."));

        if (request.Address == null ||
            string.IsNullOrWhiteSpace(request.Address.ReceiverName) ||
            string.IsNullOrWhiteSpace(request.Address.ReceiverPhone) ||
            string.IsNullOrWhiteSpace(request.Address.StreetAddress) ||
            string.IsNullOrWhiteSpace(request.Address.District) ||
            string.IsNullOrWhiteSpace(request.Address.City))
        {
            return Result<QuoteDto>.Failure(Error.Validation("InvalidAddress", "Địa chỉ giao hàng không đầy đủ."));
        }

        var version = await db.ModelVersions
            .Include(v => v.Project)
            .Include(v => v.Assets)
            .SingleOrDefaultAsync(v => v.Id == request.VersionId, ct);

        if (version == null)
            return Result<QuoteDto>.Failure(Error.NotFound("VersionNotFound", "Không tìm thấy phiên bản mô hình."));

        if (version.Project.UserId != request.UserId)
            return Result<QuoteDto>.Failure(Error.Unauthorized("NotOwner", "Chỉ chủ sở hữu của mô hình mới có quyền tạo báo giá."));

        if (request.Configuration == null)
            return Result<QuoteDto>.Failure(Error.Validation("InvalidConfiguration", "Cấu hình in không được để trống."));

        var (configHash, canonicalConfigJson) = hasher.ComputeHash(request.Configuration);

        // Kiểm tra CustomerApproval hợp lệ
        var approval = await db.CustomerApprovals
            .Where(a => a.UserId == request.UserId &&
                        a.VersionId == request.VersionId &&
                        a.ConfigurationHash == configHash)
            .OrderByDescending(a => a.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (approval == null)
            return Result<QuoteDto>.Failure(Error.Conflict("ApprovalRequired", "Chưa có phê duyệt hợp lệ của khách hàng cho cấu hình in này."));

        // Kiểm tra PrintReview Approved hợp lệ
        var review = await db.PrintReviews
            .Where(r => r.VersionId == request.VersionId &&
                        r.ConfigurationHash == configHash &&
                        r.Decision == "Approved")
            .OrderByDescending(r => r.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (review == null)
        {
            var anyApprovedReview = await db.PrintReviews
                .Where(r => r.VersionId == request.VersionId && r.Decision == "Approved")
                .OrderByDescending(r => r.CreatedAtUtc)
                .FirstOrDefaultAsync(ct);

            var isTaskApproved = false;
            Guid reviewerId = Guid.Empty;
            if (version.SourceJobId.HasValue)
            {
                var task = await db.GenerationTasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == version.SourceJobId.Value, ct);
                if (task != null && (task.IsApprovedForPrinting || task.ReviewStatus == PrintReviewStatus.Approved))
                {
                    isTaskApproved = true;
                    reviewerId = task.ReviewedByUserId ?? request.UserId;
                }
            }

            if (anyApprovedReview != null || isTaskApproved)
            {
                var autoReview = PrintReview.Create(
                    versionId: request.VersionId,
                    configurationHash: configHash,
                    reviewerUserId: anyApprovedReview?.ReviewerUserId ?? (reviewerId != Guid.Empty ? reviewerId : request.UserId),
                    decision: "Approved",
                    checklistJson: anyApprovedReview?.ChecklistJson ?? "{\"isWatertight\":true,\"hasAdequateThickness\":true,\"isPrintable\":true}",
                    evidenceStorageKey: anyApprovedReview?.EvidenceStorageKey,
                    note: anyApprovedReview?.Note ?? "Tự động phê duyệt theo phê duyệt kỹ thuật của Staff/Admin.",
                    createdAtUtc: clock.UtcNow
                );
                db.PrintReviews.Add(autoReview);
                await db.SaveChangesAsync(ct);
                review = autoReview;
            }
            else
            {
                return Result<QuoteDto>.Failure(Error.Conflict("ReviewRequired", "Chưa có phê duyệt in (Approved) của kỹ thuật viên cho cấu hình in này."));
            }
        }

        // Kiểm tra PricingPackage
        var package = await db.PricingPackages
            .SingleOrDefaultAsync(p => p.Id == request.PricingPackageId, ct);

        if (package == null || !package.IsActive)
            return Result<QuoteDto>.Failure(Error.NotFound("PackageNotFound", "Gói in không tồn tại hoặc đã bị vô hiệu hóa."));

        // Xác định chiều cao in từ config hoặc version
        decimal heightMm = version.HeightMm;
        try
        {
            using var doc = JsonDocument.Parse(canonicalConfigJson);
            if (doc.RootElement.TryGetProperty("heightMm", out var hElem) && hElem.TryGetDecimal(out var hVal))
            {
                heightMm = hVal;
            }
            else if (doc.RootElement.TryGetProperty("printHeightMm", out var phElem) && phElem.TryGetDecimal(out var phVal))
            {
                heightMm = phVal;
            }
        }
        catch
        {
            // Fallback to version.HeightMm
        }

        if (heightMm < package.MinHeightMm || heightMm > package.MaxHeightMm)
            return Result<QuoteDto>.Failure(Error.Validation("HeightOutOfRange", $"Chiều cao in ({heightMm:F1}mm) không nằm trong khoảng [{package.MinHeightMm} - {package.MaxHeightMm}mm] của gói đã chọn."));

        // Server-side financial calculations
        var unitPriceVnd = package.PriceVnd;
        var shippingFeeVnd = options.Value.DefaultShippingFeeVnd;
        var addressJson = JsonSerializer.Serialize(request.Address, JsonOptions);

        var snapshotObj = new
        {
            packageId = package.Id,
            packageCode = package.PackageCode,
            packageName = package.Name,
            material = package.Material,
            partnerBaseCostVnd = package.PartnerBaseCostVnd,
            heightMm,
            dimensions = new { widthMm = version.WidthMm, depthMm = version.DepthMm, heightMm = version.HeightMm },
            geometryHash = Convert.ToHexString(version.GeometryHash).ToLowerInvariant(),
            policyVersion = approval.PolicyVersion,
            configurationJson = canonicalConfigJson
        };
        var snapshotJson = JsonSerializer.Serialize(snapshotObj, JsonOptions);

        var now = clock.UtcNow;
        var expiresAtUtc = now.AddMinutes(options.Value.TtlMinutes);

        var quote = Quote.Create(
            request.UserId,
            request.VersionId,
            configHash,
            approval.Id,
            review.Id,
            package.Id,
            request.Quantity,
            unitPriceVnd,
            shippingFeeVnd,
            addressJson,
            snapshotJson,
            now,
            expiresAtUtc
        );

        db.Quotes.Add(quote);
        await db.SaveChangesAsync(ct);

        return Result<QuoteDto>.Success(new QuoteDto(
            quote.Id,
            quote.UserId,
            quote.VersionId,
            Convert.ToHexString(quote.ConfigurationHash).ToLowerInvariant(),
            quote.ApprovalId,
            quote.ReviewId,
            quote.PricingPackageId,
            package.PackageCode,
            package.Name,
            quote.Quantity,
            quote.UnitPriceVnd,
            quote.ShippingFeeVnd,
            quote.TotalVnd,
            request.Address,
            quote.SnapshotJson,
            quote.ExpiresAtUtc,
            quote.CreatedAtUtc,
            quote.IsExpired(now)
        ));
    }
}

