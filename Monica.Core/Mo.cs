using Microsoft.Extensions.Hosting;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Services;
using Monica.Core.TypeDiscovery.Abstractions;
using Monica.Core.TypeDiscovery.Models;

namespace Monica.Core;

public static class Mo
{
    /// <summary>
    /// Gets the shared Monica application identity defaults used by modules when their own options do not override them.
    /// Configure these defaults with <see cref="ConfigApplication"/>.
    /// </summary>
    public static IMonicaApplicationOptions Application => MonicaApplication.Current.Application;

    /// <summary>
    /// Gets shared Monica module-system defaults.
    /// Configure these defaults with <see cref="ConfigModuleSystem"/>.
    /// </summary>
    public static IMonicaModuleSystemOptions ModuleSystem => MonicaApplication.Current.ModuleSystem;

    /// <summary>
    /// Gets the global domain type finder used by the module system.
    /// Configure it with <see cref="ConfigTypeDiscovery"/>.
    /// </summary>
    public static ITypeFinder TypeFinder => MonicaApplication.Current.TypeFinder;

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

    /// <summary>
    /// Configures shared Monica application identity defaults.
    /// Module-specific options still take precedence over these global defaults.
    /// </summary>
    /// <param name="configure">The callback that mutates application identity defaults.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configure"/> is <see langword="null"/>.</exception>
    public static void ConfigApplication(Action<MonicaApplicationOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(MonicaApplication.Current.Application);
    }

    /// <summary>
    /// Configures shared Monica module-system defaults.
    /// Module-specific options still take precedence over these global defaults.
    /// </summary>
    /// <param name="configure">The callback that mutates module-system defaults.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configure"/> is <see langword="null"/>.</exception>
    public static void ConfigModuleSystem(Action<MonicaModuleSystemOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(MonicaApplication.Current.ModuleSystem);
        MonicaApplication.Current.ModuleSystem.Validate();
    }

    /// <summary>
    /// Validates the shared Monica module-system defaults.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a configured module-system value is outside its valid range.</exception>
    internal static void ValidateModuleSystem()
    {
        MonicaApplication.Current.ModuleSystem.Validate();
    }

    /// <summary>
    /// Rebuilds the global domain type finder with an optional configuration callback.
    /// Calling this without a callback resets type discovery to default <see cref="TypeFinderOptions"/>.
    /// </summary>
    /// <param name="configure">An optional callback that customizes the type finder options before creation.</param>
    public static void ConfigTypeDiscovery(Action<TypeFinderOptions>? configure = null)
    {
        MonicaApplication.Current.ConfigureTypeDiscovery(configure);
    }
}
