using MediatR;
using Microsoft.EntityFrameworkCore;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Application.Common.Models;
using TheBoys.Domain.Entities;

namespace TheBoys.Application.Approvals.Commands.CreateCustomerApproval;

public sealed class CreateCustomerApprovalCommandHandler(
    IApplicationDbContext db,
    IConfigurationHasher hasher,
    IAuditService audit,
    IClock clock) : IRequestHandler<CreateCustomerApprovalCommand, Result<CustomerApprovalDto>>
{
    public async Task<Result<CustomerApprovalDto>> Handle(CreateCustomerApprovalCommand request, CancellationToken ct)
    {
        var version = await db.ModelVersions
            .Include(v => v.Project)
            .SingleOrDefaultAsync(v => v.Id == request.VersionId, ct);

        if (version == null)
            return Result<CustomerApprovalDto>.Failure(Error.NotFound("VersionNotFound", "Không tìm thấy phiên bản mô hình."));

        if (version.Project.UserId != request.UserId)
            return Result<CustomerApprovalDto>.Failure(Error.Unauthorized("NotOwner", "Chỉ chủ sở hữu của mô hình mới có quyền phê duyệt in."));

        if (request.Configuration == null)
            return Result<CustomerApprovalDto>.Failure(Error.Validation("InvalidConfiguration", "Cấu hình in không được để trống."));

        var (hash, canonicalJson) = hasher.ComputeHash(request.Configuration);
        var policyVersion = string.IsNullOrWhiteSpace(request.PolicyVersion) ? "v1.0" : request.PolicyVersion.Trim();

        var approval = CustomerApproval.Create(
            request.UserId,
            request.VersionId,
            hash,
            canonicalJson,
            policyVersion,
            clock.UtcNow
        );

        db.CustomerApprovals.Add(approval);
        await db.SaveChangesAsync(ct);

        var hexHash = Convert.ToHexString(hash).ToLowerInvariant();

        await audit.RecordAsync(
            request.UserId,
            "CustomerApproval.Created",
            "ModelVersion",
            request.VersionId,
            Guid.NewGuid().ToString("N"),
            new { versionId = request.VersionId, configurationHash = hexHash, policyVersion },
            "Customer approved model configuration for 3D printing.",
            ct
        );

        return Result<CustomerApprovalDto>.Success(new CustomerApprovalDto(
            approval.Id,
            approval.UserId,
            approval.VersionId,
            hexHash,
            approval.ConfigurationJson,
            approval.PolicyVersion,
            approval.CreatedAtUtc
        ));
    }
}

