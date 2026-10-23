using TheBoys.Application.Common.Interfaces;

namespace TheBoys.Infrastructure.Services;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}

