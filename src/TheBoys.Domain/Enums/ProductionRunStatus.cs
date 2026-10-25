namespace TheBoys.Domain.Enums;

public enum ProductionRunStatus
{
    Printing = 1,
    PostProcessing = 2,
    QualityCheck = 3,
    Passed = 4,
    Failed = 5,
    Cancelled = 6
}

