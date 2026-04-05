using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Services;
using Monica.Core.TypeDiscovery.Abstractions;
using Monica.Core.TypeDiscovery.Models;
using Monica.Core.TypeDiscovery.Services;

namespace Monica.Core;

public static class Mo
{
    /// <summary>
    /// Registers already-loaded modules immediately.
    /// Use this when some modules must be available during registration, such as configuration or logging.
    /// Be careful with modules that have nested dependencies because later configuration may no longer take effect
    /// after those modules have already been registered.
    /// </summary>
    /// <param name="builder">The host application builder used to register module services.</param>
    public static void RegisterInstantly(IHostApplicationBuilder builder)
    {
        ModuleRegistry.RegisterServices(builder);
    }

    public static class Options
    {
        /// <summary>
        /// Gets or sets the default log level used by modules.
        /// </summary>
        public static LogLevel DefaultModuleLogLevel { get; set; } = LogLevel.Information;

        /// <summary>
        /// Gets or sets whether a module should be disabled instead of throwing when registration fails.
        /// When enabled, the system logs the failure and skips the module for the rest of the application lifetime
        /// instead of aborting startup.
        /// </summary>
        public static bool DisableModuleIfHasException { get; set; }

        /// <summary>
        /// Gets or sets whether module execution summary logs are emitted after module system initialization.
        /// </summary>
        public static bool EnableLoggingModuleSummary { get; set; }

        /// <summary>
        /// Gets or sets the default Swagger group name for modules that expose endpoints.
        /// The module name is used when this value is not set.
        /// </summary>
        public static string? DefaultModuleApiGroupName { get; set; }

        /// <summary>
        /// Gets or sets the default Minimal API disabled state for modules.
        /// When this value is <see langword="null"/>, modules use their own defaults.
        /// </summary>
        public static bool? DefaultMinimalApiDisabled { get; set; }

        /// <summary>
        /// Gets the global domain type finder used by the module system.
        /// </summary>
        public static ITypeFinder GlobalTypeFinder => _globalTypeFinder ??= new DomainTypeFinder(new TypeFinderOptions());

        private static ITypeFinder? _globalTypeFinder;

        /// <summary>
        /// Rebuilds the global domain type finder with an optional configuration callback.
        /// </summary>
        /// <param name="configure">An optional callback that customizes the type finder options before creation.</param>
        public static void ConfigTypeFinder(Action<TypeFinderOptions>? configure = null)
        {
            var option = new TypeFinderOptions();
            configure?.Invoke(option);
            _globalTypeFinder = new DomainTypeFinder(option);
        }
    }
}
