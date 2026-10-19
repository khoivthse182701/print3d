using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Application.Common.Models;
using TheBoys.Application.Printing;
using TheBoys.Domain.Entities;

namespace TheBoys.Application.Generations.Queries;

public sealed record GetGenerationTasksQuery(
    Guid UserId,
    bool IsManagement,
    int Page = 1
) : IRequest<IReadOnlyList<GenerationTask>>;

public sealed class GetGenerationTasksQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetGenerationTasksQuery, IReadOnlyList<GenerationTask>>
{
    public async Task<IReadOnlyList<GenerationTask>> Handle(GetGenerationTasksQuery request, CancellationToken ct)
    {
        var query = db.GenerationTasks.AsNoTracking();
        if (!request.IsManagement) query = query.Where(x => x.UserId == request.UserId);
        return await query.OrderByDescending(x => x.CreatedAtUtc)
            .Skip((request.Page - 1) * 20)
            .Take(20)
            .ToListAsync(ct);
    }
}

public sealed record GetGenerationTaskByIdQuery(
    Guid Id,
    Guid UserId,
    bool IsManagement
) : IRequest<Result<GenerationTask>>;

public sealed class GetGenerationTaskByIdQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetGenerationTaskByIdQuery, Result<GenerationTask>>
{
    public async Task<Result<GenerationTask>> Handle(GetGenerationTaskByIdQuery request, CancellationToken ct)
    {
        var task = await db.GenerationTasks.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.Id, ct);
        if (task == null || (!request.IsManagement && task.UserId != request.UserId))
            return Result<GenerationTask>.Failure(Error.NotFound("TaskNotFound", "Không tìm thấy tác vụ."));
        return Result<GenerationTask>.Success(task);
    }
}

public sealed record GetPrintQuoteQuery(
    Guid TaskId,
    Guid PackageId,
    int Quantity,
    Guid UserId,
    bool IsManagement
) : IRequest<Result<PrintQuote>>;

public sealed class GetPrintQuoteQueryHandler(
    IApplicationDbContext db,
    PrintPricingService pricing,
    IOptions<PrintPricingOptions> pricingOptions) : IRequestHandler<GetPrintQuoteQuery, Result<PrintQuote>>
{
    public async Task<Result<PrintQuote>> Handle(GetPrintQuoteQuery request, CancellationToken ct)
    {
        var task = await db.GenerationTasks.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.TaskId, ct);
        if (task == null || (!request.IsManagement && task.UserId != request.UserId))
            return Result<PrintQuote>.Failure(Error.NotFound("TaskNotFound", "Không tìm thấy tác vụ."));

        var package = await db.PricingPackages.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.PackageId, ct);
        if (package == null)
            return Result<PrintQuote>.Failure(Error.NotFound("PackageNotFound", "Không tìm thấy gói in."));

        try
        {
            var quote = pricing.Quote(task, package, request.Quantity, pricingOptions.Value);
            return Result<PrintQuote>.Success(quote);
        }
        catch (ArgumentException ex)
        {
            return Result<PrintQuote>.Failure(Error.Validation("InvalidQuote", ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Result<PrintQuote>.Failure(Error.Conflict("CannotQuote", ex.Message));
        }
    }
}
