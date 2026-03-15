using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using Monica.Core.Module;
using Monica.Core.Module.BuilderWrapper;
using Monica.Core.Module.TypeFinder;

namespace Monica.Core;

public static class Mo
{
    /// <summary>
    /// Initializes the Mo module system with Harmony patches for ASP.NET Core lifecycle interception.
    /// This should be called early in the application startup, typically before configuring services.
    /// </summary>
    public static void Initialize()
    {
        // Initialize Harmony patches to intercept native ASP.NET Core methods
        HarmonyPatchManager.EnsurePatched();
    }

    /// <summary>
    /// Registers already-loaded modules immediately.
    /// Use this when some modules must be available during registration, such as configuration or logging.
    /// Be careful with modules that have nested dependencies because later configuration may no longer take effect
    /// after those modules have already been registered.
    /// </summary>
    /// <param name="builder">The application builder used to register module services.</param>
    public static void RegisterInstantly(WebApplicationBuilder builder)
    {
        MoModuleRegisterCentre.RegisterServices(builder);
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
        public static IDomainTypeFinder GlobalTypeFinder => _globalTypeFinder ??= new MoDomainTypeFinder(new ModuleCoreOptionTypeFinder());

        private static IDomainTypeFinder? _globalTypeFinder;

        /// <summary>
        /// Rebuilds the global domain type finder with an optional configuration callback.
        /// </summary>
        /// <param name="configure">An optional callback that customizes the type finder options before creation.</param>
        public static void ConfigTypeFinder(Action<ModuleCoreOptionTypeFinder>? configure = null)
        {
            var option = new ModuleCoreOptionTypeFinder();
            configure?.Invoke(option);
            _globalTypeFinder = new MoDomainTypeFinder(option);
        }
    }
}
