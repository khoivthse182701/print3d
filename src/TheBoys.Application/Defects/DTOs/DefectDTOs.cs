namespace TheBoys.Application.Defects.DTOs;

public sealed record DefectEvidenceDto(
    Guid Id,
    Guid ReportId,
    string StorageKey,
    string Sha256Hex,
    DateTime CreatedAtUtc
);

public sealed record DefectReportDto(
    Guid Id,
    Guid OrderId,
    Guid UserId,
    Guid ClientRequestId,
    string Reason,
    string Status,
    string? ResolutionNote,
    Guid? ResolvedByUserId,
    Guid? RefundId,
    Guid? ReprintRunId,
    DateTime CreatedAtUtc,
    DateTime? ResolvedAtUtc,
    List<DefectEvidenceDto> Evidence
);

