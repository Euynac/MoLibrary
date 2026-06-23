using Monica.Configuration.EventBus.Modules;
using Monica.Core;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

/// <summary>
/// Builder extensions for the Monica.Configuration EventBus bridge module.
/// </summary>
public static class ModuleConfigurationEventBusBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Registers the optional EventBus bridge for distributed Monica configuration reload signals.
        /// </summary>
        /// <param name="action">Optional bridge option configuration.</param>
        /// <returns>The module guide.</returns>
        public static ModuleConfigurationEventBusGuide AddConfigurationEventBus(
            Action<ModuleConfigurationEventBusOption>? action = null)
        {
            return new ModuleConfigurationEventBusGuide().Register(action);
        }
    }
}
