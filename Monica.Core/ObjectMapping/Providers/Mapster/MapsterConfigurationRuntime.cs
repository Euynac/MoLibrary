using Mapster;

namespace Monica.Core.ObjectMapping.Providers.Mapster;

/// <summary>
/// Owns the Mapster configuration lifecycle for one Monica host.
/// </summary>
/// <remarks>
/// The initial snapshot remains available for lazy mapping while Monica compiles an isolated clone. Publishing the
/// compiled clone is atomic, so an individual mapping operation always observes one complete configuration.
/// </remarks>
internal sealed class MapsterConfigurationRuntime
{
    private readonly TypeAdapterConfig _fallbackConfiguration = new();
    private TypeAdapterConfig _currentConfiguration;
    private int _isFrozen;

    internal MapsterConfigurationRuntime()
    {
        _currentConfiguration = _fallbackConfiguration;
    }

    /// <summary>
    /// Gets the configuration snapshot that new mapping operations should use.
    /// </summary>
    internal TypeAdapterConfig CurrentConfiguration => Volatile.Read(ref _currentConfiguration);

    /// <summary>
    /// Gets whether background compilation has published its validated configuration.
    /// </summary>
    internal bool HasPublishedCompiledConfiguration =>
        !ReferenceEquals(CurrentConfiguration, _fallbackConfiguration);

    /// <summary>
    /// Applies all host-owned profiles before the configuration is exposed to runtime consumers.
    /// </summary>
    /// <param name="configure">The synchronous profile-composition action.</param>
    internal void Configure(Action<TypeAdapterConfig> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        if (Volatile.Read(ref _isFrozen) != 0)
        {
            throw new InvalidOperationException("The host-owned Mapster configuration is already frozen.");
        }

        configure(_fallbackConfiguration);
    }

    /// <summary>
    /// Freezes profile composition and creates the isolated candidate used for eager compilation.
    /// </summary>
    /// <returns>A private configuration clone that may be compiled without mutating the lazy fallback.</returns>
    internal TypeAdapterConfig FreezeAndCreateCompilationCandidate()
    {
        if (Interlocked.Exchange(ref _isFrozen, 1) != 0)
        {
            throw new InvalidOperationException("The host-owned Mapster configuration is already frozen.");
        }

        return _fallbackConfiguration.Clone();
    }

    /// <summary>
    /// Publishes a fully compiled configuration for subsequent mapping operations.
    /// </summary>
    /// <param name="compiledConfiguration">The isolated configuration after successful eager compilation.</param>
    internal void PublishCompiled(TypeAdapterConfig compiledConfiguration)
    {
        ArgumentNullException.ThrowIfNull(compiledConfiguration);
        Interlocked.Exchange(ref _currentConfiguration, compiledConfiguration);
    }
}
