namespace TheBoys.Domain.Enums;

public enum IdempotencyStatus
{
    Processing = 1,
    Completed = 2,
    NeedsAttention = 3
}

