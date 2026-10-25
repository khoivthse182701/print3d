using TheBoys.Domain.Enums;

namespace TheBoys.WebApi.Contracts.Staff;

public sealed record StaffGenerationJobResponse(
    Guid Id,
    string? ModelName,
    string Provider,
    GenerationStatus Status,
    GenerationStage Stage,
    int Progress,
    PrintReviewStatus ReviewStatus,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc,
    long AgeMinutes
);
