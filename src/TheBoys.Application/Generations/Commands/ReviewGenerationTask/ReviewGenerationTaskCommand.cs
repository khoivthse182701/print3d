using MediatR;
using Microsoft.EntityFrameworkCore;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Application.Common.Models;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;

namespace TheBoys.Application.Generations.Commands.ReviewGenerationTask;

public sealed record ReviewGenerationTaskCommand(
    Guid TaskId,
    Guid ReviewerUserId,
    bool Approved,
    string Note
) : IRequest<Result<GenerationTask>>;

public sealed class ReviewGenerationTaskCommandHandler(IApplicationDbContext db)
    : IRequestHandler<ReviewGenerationTaskCommand, Result<GenerationTask>>
{
    public async Task<Result<GenerationTask>> Handle(ReviewGenerationTaskCommand request, CancellationToken ct)
    {
        var task = await db.GenerationTasks.SingleOrDefaultAsync(x => x.Id == request.TaskId, ct);
        if (task == null) return Result<GenerationTask>.Failure(Error.NotFound("TaskNotFound", "Không tìm thấy tác vụ."));
        if (task.Status != GenerationStatus.Succeeded)
            return Result<GenerationTask>.Failure(Error.Conflict("TaskNotSucceeded", "Model chưa hoàn tất."));

        if (request.Approved && (!task.IsWatertight || task.PrintabilityStatus is not ("healthy" or "warning") || task.StoragePathStl == null))
            return Result<GenerationTask>.Failure(Error.Conflict("MeshCheckFailed", "Model chưa đạt kiểm tra mesh/độ dày. Cần sửa file hoặc tạo mẫu khác trước khi duyệt."));

        if (string.IsNullOrWhiteSpace(request.Note))
            return Result<GenerationTask>.Failure(Error.Validation("InvalidNote", "Ghi chú duyệt không được để trống."));

        task.IsApprovedForPrinting = request.Approved;
        task.ReviewStatus = request.Approved ? PrintReviewStatus.Approved : PrintReviewStatus.Rejected;
        task.ReviewedByUserId = request.ReviewerUserId;
        task.ReviewedAtUtc = DateTime.UtcNow;
        task.ReviewNote = request.Note.Trim();

        await db.SaveChangesAsync(ct);
        return Result<GenerationTask>.Success(task);
    }
}
