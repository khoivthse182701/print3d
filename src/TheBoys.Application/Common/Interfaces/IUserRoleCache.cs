namespace TheBoys.Application.Common.Interfaces;

public interface IUserRoleCache
{
    string? Get(Guid userId);
    void Set(Guid userId, string role, TimeSpan? expiration = null);
    void Remove(Guid userId);
}
