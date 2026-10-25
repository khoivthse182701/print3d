using MediatR;
using TheBoys.Application.Staff.Models;
using TheBoys.Domain.Enums;

namespace TheBoys.Application.Staff.Queries.GetStaffGenerationJobs;

public sealed record GetStaffGenerationJobsQuery(
    GenerationStatus? Status,
    PrintReviewStatus? ReviewStatus,
    string? Provider,
    DateTime? CreatedFromUtc,
    DateTime? CreatedToUtc,
    int? MinAgeMinutes,
    int Page,
    int PageSize,
    string Sort,
    DateTime CapturedUtcNow
) : IRequest<StaffPagedResult<StaffGenerationJobItem>>;
