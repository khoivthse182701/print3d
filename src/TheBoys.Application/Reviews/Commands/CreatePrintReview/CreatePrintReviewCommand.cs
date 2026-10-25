using MediatR;
using TheBoys.Application.Common.Models;

namespace TheBoys.Application.Reviews.Commands.CreatePrintReview;

public sealed record CreatePrintReviewCommand(
    Guid VersionId,
    Guid ReviewerUserId,
    string Decision,
    PrintReviewChecklistDto Checklist,
    object Configuration,
    string? EvidenceStorageKey,
    string Note
) : IRequest<Result<PrintReviewDto>>;

