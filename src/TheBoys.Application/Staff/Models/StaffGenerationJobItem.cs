using TheBoys.Domain.Enums;

namespace TheBoys.Application.Staff.Models;

public sealed record StaffGenerationJobItem(
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
