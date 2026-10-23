namespace TheBoys.Application.Common.Interfaces;

public interface IConsentService
{
    Task<bool> HasValidConsentAsync(Guid userId, string purpose, string policyVersion, CancellationToken ct);
    Task GrantConsentAsync(Guid userId, string purpose, string policyVersion, CancellationToken ct);
    Task RevokeConsentAsync(Guid userId, string purpose, string policyVersion, CancellationToken ct);
}

