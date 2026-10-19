using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;

namespace TheBoys.Application.Printing;

public static class GenerationOperations
{
    public static string? ProviderId(this GenerationTask task) => task.Stage switch
    {
        GenerationStage.Generate => task.ProviderTaskId,
        GenerationStage.Resize => task.ResizeTaskId,
        GenerationStage.Convert => task.ConvertTaskId,
        GenerationStage.Analyze => task.AnalysisTaskId,
        _ => null
    };
    public static void SetProviderId(this GenerationTask task, string id)
    {
        switch (task.Stage)
        {
            case GenerationStage.Generate: task.ProviderTaskId = id; break;
            case GenerationStage.Resize: task.ResizeTaskId = id; break;
            case GenerationStage.Convert: task.ConvertTaskId = id; break;
            case GenerationStage.Analyze: task.AnalysisTaskId = id; break;
            default: throw new InvalidOperationException("Task đã hoàn tất.");
        }
    }
}
