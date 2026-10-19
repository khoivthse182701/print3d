using Microsoft.Extensions.Caching.Memory;
using TheBoys.Application.Common.Interfaces;

namespace TheBoys.Infrastructure.Services;

public sealed class UserRoleCache(IMemoryCache cache) : IUserRoleCache
{
    private static string CacheKey(Guid userId) => $"user_role_{userId:N}";

    public string? Get(Guid userId)
    {
        return cache.TryGetValue(CacheKey(userId), out string? role) ? role : null;
    }

    public void Set(Guid userId, string role, TimeSpan? expiration = null)
    {
        cache.Set(CacheKey(userId), role, expiration ?? TimeSpan.FromSeconds(60));
    }

    public void Remove(Guid userId)
    {
        cache.Remove(CacheKey(userId));
    }
}
