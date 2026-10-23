namespace TheBoys.Application.Printing;

public interface IModelProviderRegistry
{
    IModelGenerationProvider Resolve(string providerKey);
    IModelGenerationProvider ResolveForNewJob(string? requestedProviderKey = null, string? requestedCapability = null);
    ProviderCapabilities GetCapabilities(string providerKey, string? modelKey = null);
    IReadOnlyList<string> GetAvailableProviderKeys();
}

