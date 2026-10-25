namespace TheBoys.Application.Approvals;

public sealed record CustomerApprovalDto(
    Guid Id,
    Guid UserId,
    Guid VersionId,
    string ConfigurationHash,
    string ConfigurationJson,
    string PolicyVersion,
    DateTime CreatedAtUtc
);

