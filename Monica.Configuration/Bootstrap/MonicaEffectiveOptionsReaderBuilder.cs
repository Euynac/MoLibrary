using Microsoft.Extensions.Configuration;
using Monica.Configuration.Abstractions;

namespace Monica.Configuration.Bootstrap;

/// <summary>
/// Builds a startup reader for Monica effective options.
/// </summary>
public sealed class MonicaEffectiveOptionsReaderBuilder
{
    private readonly IConfiguration _bootstrapConfiguration;
    private readonly MonicaEffectiveOptionsReaderOptions _options;
    private Func<IConfigurationEffectiveValueStore>? _storeFactory;

    internal MonicaEffectiveOptionsReaderBuilder(
        IConfiguration bootstrapConfiguration,
        MonicaEffectiveOptionsReaderOptions options)
    {
        _bootstrapConfiguration = bootstrapConfiguration;
        _options = options;
    }

    /// <summary>
    /// Uses the effective-value store returned by the factory.
    /// </summary>
    /// <param name="factory">
    /// Factory for the store used by the reader. The reader owns the returned instance and disposes it when possible.
    /// </param>
    /// <returns>The same builder for chaining.</returns>
    public MonicaEffectiveOptionsReaderBuilder UseEffectiveValueStore(Func<IConfigurationEffectiveValueStore> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _storeFactory = factory;
        return this;
    }

    /// <summary>
    /// Builds the startup effective-options reader.
    /// </summary>
    /// <returns>The effective-options reader.</returns>
    public IMonicaEffectiveOptionsReader Build()
    {
        if (_storeFactory is null)
        {
            throw new InvalidOperationException(
                $"No Monica effective-value store has been configured. Call {nameof(UseEffectiveValueStore)} before {nameof(Build)}.");
        }

        var store = _storeFactory.Invoke();
        return new MonicaEffectiveOptionsReader(_bootstrapConfiguration, _options, store);
    }
}
