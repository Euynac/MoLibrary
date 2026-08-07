using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace Monica.Configuration.Bootstrap;

/// <summary>
/// Represents a caller-owned bootstrap configuration root created from a
/// <see cref="MonicaConfigurationInputPlan"/>.
/// </summary>
/// <remarks>
/// Dispose this root after startup snapshot loading completes to release file watchers and chained-provider
/// registrations. Disposing it does not dispose the host's configuration root.
/// </remarks>
public sealed class MonicaBootstrapConfiguration : IConfigurationRoot, IDisposable
{
    private readonly MonicaConfigurationInputPlan _inputPlan;
    private readonly IConfigurationRoot _configuration;
    private bool _disposed;

    internal MonicaBootstrapConfiguration(
        MonicaConfigurationInputPlan inputPlan,
        IConfiguration hostConfiguration,
        string contentRootPath,
        IConfigurationRoot configuration)
    {
        _inputPlan = inputPlan;
        HostConfiguration = hostConfiguration;
        ContentRootPath = contentRootPath;
        _configuration = configuration;
    }

    internal IConfiguration HostConfiguration { get; }

    internal string ContentRootPath { get; }

    /// <inheritdoc />
    public string? this[string key]
    {
        get => _configuration[key];
        set => _configuration[key] = value;
    }

    /// <inheritdoc />
    public IEnumerable<IConfigurationProvider> Providers => _configuration.Providers;

    /// <inheritdoc />
    public IEnumerable<IConfigurationSection> GetChildren() => _configuration.GetChildren();

    /// <inheritdoc />
    public IChangeToken GetReloadToken() => _configuration.GetReloadToken();

    /// <inheritdoc />
    public IConfigurationSection GetSection(string key) => _configuration.GetSection(key);

    /// <inheritdoc />
    public void Reload() => _configuration.Reload();

    /// <summary>
    /// Releases resources owned by the bootstrap configuration providers.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        (_configuration as IDisposable)?.Dispose();
    }

    internal void EnsureOwnedBy(MonicaConfigurationInputPlan inputPlan)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!ReferenceEquals(_inputPlan, inputPlan))
        {
            throw new InvalidOperationException(
                "The bootstrap configuration was created by a different Monica Configuration input plan.");
        }
    }
}
