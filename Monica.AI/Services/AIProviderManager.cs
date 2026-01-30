using System.Collections.Concurrent;
using Monica.AI.Abstractions;
using Monica.AI.Models;

namespace Monica.AI.Services;

/// <summary>
/// AI Provider 管理器实现
/// </summary>
public class AIProviderManager : IAIProviderFactory, IDisposable
{
    private readonly ConcurrentDictionary<string, IAIProvider> _providers = new();
    private string? _defaultProviderId;
    private bool _disposed;

    /// <summary>
    /// 注册 Provider
    /// </summary>
    /// <param name="provider">Provider 实例</param>
    public void RegisterProvider(IAIProvider provider)
    {
        _providers[provider.ProviderId] = provider;

        // 如果是默认 Provider 或者是第一个注册的 Provider
        if (provider.Info.IsDefault || _defaultProviderId == null)
        {
            _defaultProviderId = provider.ProviderId;
        }
    }

    /// <summary>
    /// 设置默认 Provider
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
