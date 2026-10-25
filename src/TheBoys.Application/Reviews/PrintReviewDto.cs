namespace TheBoys.Application.Reviews;

public sealed record PrintReviewDto(
    Guid Id,
    Guid VersionId,
    string ConfigurationHash,
    Guid ReviewerUserId,
    string Decision,
    string ChecklistJson,
    string? EvidenceStorageKey,
    string Note,
    DateTime CreatedAtUtc
);

