using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;

namespace Monica.Configuration.Bootstrap;

/// <summary>
/// Collects the explicit, caller-owned inputs used to create one startup effective-options reader.
/// </summary>
/// <remarks>
/// Instances are intentionally independent from module registration. Keep one instance within the host-composition
/// scope that creates the reader; sharing an instance across hosts would also share its store factory and source list.
/// </remarks>
public sealed class MonicaEffectiveOptionsReaderConfiguration
{
    private readonly List<ManagedJsonConfigurationSourceRegistration> _managedJsonSources = [];
    private Func<IConfigurationEffectiveValueStore>? _storeFactory;

    /// <summary>
    /// Adds a managed JSON source to the reader's precedence chain.
    /// </summary>
    /// <param name="registration">The immutable source registration to append.</param>
    /// <returns>This configuration instance.</returns>
    public MonicaEffectiveOptionsReaderConfiguration AddManagedJsonSource(
        ManagedJsonConfigurationSourceRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        _managedJsonSources.Add(registration);
        return this;
    }

    /// <summary>
    /// Selects the factory that creates the reader-owned effective-value store.
    /// </summary>
    /// <param name="factory">
    /// A factory invoked once for each created reader. The reader owns and disposes the returned store when supported.
    /// </param>
    /// <returns>This configuration instance.</returns>
    public MonicaEffectiveOptionsReaderConfiguration UseEffectiveValueStore(
        Func<IConfigurationEffectiveValueStore> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _storeFactory = factory;
        return this;
    }

    internal IMonicaEffectiveOptionsReader CreateReader(
        IHostApplicationBuilder hostBuilder,
        IConfiguration bootstrapConfiguration,
        Action<MonicaEffectiveOptionsReaderOptions>? configure,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(hostBuilder);
        ArgumentNullException.ThrowIfNull(bootstrapConfiguration);
        ArgumentNullException.ThrowIfNull(logger);

        if (_storeFactory is null)
        {
            throw new InvalidOperationException(
                "No Monica effective-value store has been configured for the startup options reader. Call UseFileConfigurationStore or UseDbConfigurationStore before CreateEffectiveOptionsReader.");
        }

        var options = new MonicaEffectiveOptionsReaderOptions();
        configure?.Invoke(options);

        return new MonicaEffectiveOptionsReader(
            hostBuilder,
            bootstrapConfiguration,
            options,
            _storeFactory.Invoke(),
            _managedJsonSources,
            logger);
    }
}
