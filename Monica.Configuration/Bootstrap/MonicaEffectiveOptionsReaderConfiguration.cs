using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;

namespace Monica.Configuration.Bootstrap;

internal sealed class MonicaEffectiveOptionsReaderConfiguration
{
    private readonly List<ManagedJsonConfigurationSourceRegistration> _managedJsonSources = [];
    private Func<IConfigurationEffectiveValueStore>? _storeFactory;

    public void AddManagedJsonSource(ManagedJsonConfigurationSourceRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        _managedJsonSources.Add(registration);
    }

    public void UseEffectiveValueStore(Func<IConfigurationEffectiveValueStore> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _storeFactory = factory;
    }

    public IMonicaEffectiveOptionsReader CreateReader(
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
