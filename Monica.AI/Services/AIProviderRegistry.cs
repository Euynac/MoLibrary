using Monica.AI.Abstractions;
using Monica.AI.Models;

namespace Monica.AI.Services;

/// <summary>
/// AI provider manager implementation.
/// </summary>
internal sealed class AIProviderRegistry(IEnumerable<IAIProvider> providers) : IAIProviderFactory
{
    private readonly ProviderSnapshot _snapshot = CreateSnapshot(providers);

    private IReadOnlyDictionary<string, IAIProvider> Providers => _snapshot.Providers;

    /// <inheritdoc />
    public IAIProvider? GetProvider(string providerId)
    {
        return Providers.GetValueOrDefault(providerId);
    }

    /// <inheritdoc />
    public IReadOnlyList<IAIProvider> GetAllProviders()
    {
        return Providers.Values.ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<AIProviderInfo> GetAllProviderInfos()
    {
        return Providers.Values.Select(static provider => provider.Info).ToList();
    }

    /// <inheritdoc />
    public IAIProvider? GetDefaultProvider()
    {
        if (_snapshot.DefaultProviderId is not null)
        {
            return Providers.GetValueOrDefault(_snapshot.DefaultProviderId);
        }

        return null;
    }

    /// <inheritdoc />
    public bool HasProvider(string providerId)
    {
        return Providers.ContainsKey(providerId);
    }

    private static ProviderSnapshot CreateSnapshot(IEnumerable<IAIProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        var providerList = providers.ToList();
        var duplicates = providerList
            .GroupBy(static provider => provider.ProviderId, StringComparer.OrdinalIgnoreCase)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .OrderBy(static id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (duplicates.Count > 0)
        {
            throw new InvalidOperationException(
                $"Duplicate AI provider identifiers are not allowed: {string.Join(", ", duplicates)}.");
        }

        var providerMap = providerList.ToDictionary(
            static provider => provider.ProviderId,
            StringComparer.OrdinalIgnoreCase);
        var validProviders = providerList.Where(static provider => provider.Info.IsValid).ToList();
        var explicitDefaults = validProviders.Where(static provider => provider.Info.IsDefault).ToList();
        if (explicitDefaults.Count > 1)
        {
            throw new InvalidOperationException(
                $"Only one valid AI provider may be configured as the default: {string.Join(", ", explicitDefaults.Select(static provider => provider.ProviderId))}.");
        }

        var defaultProviderId = explicitDefaults.FirstOrDefault()?.ProviderId ?? validProviders.FirstOrDefault()?.ProviderId;
        return new ProviderSnapshot(providerMap, defaultProviderId);
    }

    private sealed record ProviderSnapshot(
        IReadOnlyDictionary<string, IAIProvider> Providers,
        string? DefaultProviderId);
}
