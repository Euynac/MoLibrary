using Monica.Configuration.EventBus.Modules;
using Monica.Core.Modularity.Abstractions;
using Monica.EventBus.Abstractions;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

/// <summary>
/// Builder extensions that attach the EventBus bridge to Monica.Configuration.
/// </summary>
public static class ModuleConfigurationEventBusBuilderExtensions
{
    /// <summary>
    /// Registers the optional EventBus bridge for distributed Monica configuration reload signals.
    /// </summary>
    /// <param name="module">The Configuration registration that will include the EventBus bridge.</param>
    /// <param name="action">Optional bridge option configuration.</param>
    /// <returns>The EventBus bridge module registration.</returns>
    public static ModuleRegistration<ModuleConfigurationEventBus, ModuleConfigurationEventBusOption> UseEventBus(
        this ModuleRegistration<ModuleConfiguration, ModuleConfigurationOption> module,
        Action<ModuleConfigurationEventBusOption>? action = null)
    {
        ArgumentNullException.ThrowIfNull(module);
        return module.Include<ModuleConfigurationEventBus, ModuleConfigurationEventBusOption>(action);
    }

    /// <summary>
    /// Registers the EventBus bridge and binds it to a keyed distributed EventBus provider.
    /// </summary>
    /// <param name="module">The Configuration registration that will include the EventBus bridge.</param>
    /// <param name="distributedEventBusServiceKey">The keyed <see cref="IDistributedEventBus"/> service key.</param>
    /// <param name="action">Optional bridge option configuration.</param>
    /// <returns>The EventBus bridge module registration.</returns>
    public static ModuleRegistration<ModuleConfigurationEventBus, ModuleConfigurationEventBusOption> UseEventBus(
        this ModuleRegistration<ModuleConfiguration, ModuleConfigurationOption> module,
        string distributedEventBusServiceKey,
        Action<ModuleConfigurationEventBusOption>? action = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(distributedEventBusServiceKey);

        return module.UseEventBus(options =>
        {
            action?.Invoke(options);
            options.DistributedEventBusServiceKey = distributedEventBusServiceKey;
        });
    }
}
