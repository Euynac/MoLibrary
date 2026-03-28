using System.Collections.Concurrent;
using Monica.AI.Abstractions;
using Monica.AI.Models;

namespace Monica.AI.Services;

/// <summary>
/// AI provider manager implementation.
/// </summary>
public class AIProviderManager : IAIProviderFactory, IDisposable
{
    private readonly ConcurrentDictionary<string, IAIProvider> _providers = new();
    private string? _defaultProviderId;
    private bool _disposed;

    /// <summary>
    /// Registers a provider.
    /// </summary>
    /// <param name="provider">Provider instance</param>
    public void RegisterProvider(IAIProvider provider)
    {
        _providers[provider.ProviderId] = provider;

        // If this is the default provider or the first registered provider
        if (provider.Info.IsDefault || _defaultProviderId == null)
        {
            _defaultProviderId = provider.ProviderId;
        }
    }

    /// <summary>
    /// Sets the default provider.
    /// </summary>
    /// <param name="providerId">Provider ID</param>
    public void SetDefaultProvider(string providerId)
    {
        if (_providers.ContainsKey(providerId))
        {
            _defaultProviderId = providerId;
        }
    }

    /// <inheritdoc />
    public IAIProvider? GetProvider(string providerId)
    {
        return _providers.GetValueOrDefault(providerId);
    }

    /// <inheritdoc />
    public IReadOnlyList<IAIProvider> GetAllProviders()
    {
        return _providers.Values.ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<AIProviderInfo> GetAllProviderInfos()
    {
        return _providers.Values.Select(p => p.Info).ToList();
    }

    /// <inheritdoc />
    public IAIProvider? GetDefaultProvider()
    {
        if (_defaultProviderId == null)
        {
            return _providers.Values.FirstOrDefault();
        }

        return _providers.GetValueOrDefault(_defaultProviderId);
    }

    /// <inheritdoc />
    public bool HasProvider(string providerId)
    {
        return _providers.ContainsKey(providerId);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                foreach (var provider in _providers.Values)
                {
                    provider.Dispose();
                }
                _providers.Clear();
            }
            _disposed = true;
        }
    }
}
