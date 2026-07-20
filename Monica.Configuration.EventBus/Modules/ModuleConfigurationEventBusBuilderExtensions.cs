using Monica.Configuration.EventBus.Modules;
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
    /// <param name="guide">The configuration module guide.</param>
    /// <param name="action">Optional bridge option configuration.</param>
    /// <returns>The EventBus bridge module guide.</returns>
    public static ModuleConfigurationEventBusGuide UseEventBus(
        this ModuleConfigurationGuide guide,
        Action<ModuleConfigurationEventBusOption>? action = null)
    {
        ArgumentNullException.ThrowIfNull(guide);
        return guide.AddModule<ModuleConfigurationEventBus, ModuleConfigurationEventBusOption, ModuleConfigurationEventBusGuide>(action);
    }

    /// <summary>
    /// Registers the EventBus bridge and binds it to a keyed distributed EventBus provider.
    /// </summary>
    /// <param name="guide">The configuration module guide.</param>
    /// <param name="distributedEventBusServiceKey">The keyed <see cref="IDistributedEventBus"/> service key.</param>
    /// <param name="action">Optional bridge option configuration.</param>
    /// <returns>The EventBus bridge module guide.</returns>
    public static ModuleConfigurationEventBusGuide UseEventBus(
        this ModuleConfigurationGuide guide,
        string distributedEventBusServiceKey,
        Action<ModuleConfigurationEventBusOption>? action = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(distributedEventBusServiceKey);

        return guide.UseEventBus(options =>
        {
            action?.Invoke(options);
            options.DistributedEventBusServiceKey = distributedEventBusServiceKey;
        });
    }
}
