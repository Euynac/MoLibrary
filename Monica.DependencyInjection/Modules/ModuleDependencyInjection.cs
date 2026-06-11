using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.DependencyInjection.Abstractions;
using Monica.DependencyInjection.Abstractions.Internal;
using Monica.DependencyInjection.Facades;
using Monica.DependencyInjection.Services;
using Monica.DependencyInjection.Services.Support;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleDependencyInjectionBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Enables Monica conventional dependency registration and cached service-provider access.
        /// </summary>
        public static ModuleDependencyInjectionGuide AddDependencyInjection(Action<ModuleDependencyInjectionOption>? action = null)
        {
            return new ModuleDependencyInjectionGuide().Register(action);
        }
    }
}

[ModuleKey(BuiltInModuleKey.DependencyInjection)]
public class ModuleDependencyInjection(ModuleDependencyInjectionOption option)
    : ModuleBase<ModuleDependencyInjection, ModuleDependencyInjectionOption, ModuleDependencyInjectionGuide>(option), IBusinessTypeIterator
{
    private IConventionalRegistrar? _registrar;
    private DependencyInjectionDiagnosticsRegistry? _diagnosticsRegistry;
    private IServiceCollection? _services;

    public override void ConfigureServices(IServiceCollection services)
    {
        DependencyInjectionDiagnosticsRegistry? diagnosticsRegistry = null;
        if (Option.EnableAutoRegistrationDiagnostics)
        {
            _diagnosticsRegistry ??= new DependencyInjectionDiagnosticsRegistry();
            _diagnosticsRegistry.BindServices(services);

            services.AddSingleton(_diagnosticsRegistry);
            services.AddSingleton<DependencyInjectionDiagnosticsService>();
            services.AddSingleton<DependencyInjectionDiagnosticsFacade>();
            services.AddHostedService<DependencyInjectionDiagnosticsHostedService>();

            diagnosticsRegistry = _diagnosticsRegistry;
        }

        services.AddScoped<ICachedServiceProvider, CachedServiceProvider>();

        _registrar = new ConventionalRegistrar(Option, diagnosticsRegistry);
        _services = services;
    }

    /// <inheritdoc />
    public override void ClaimDependencies()
    {
        if (Option.EnableAutoRegistrationDiagnostics)
        {
            DependsOnModule<ModuleHostedServiceGuide>().Register();
        }
    }

    /// <summary>
    /// Iterates through business types and registers them with the dependency injection container.
    /// </summary>
    /// <param name="types">The collection of types to iterate through.</param>
    /// <returns>An enumerable collection of the processed types.</returns>
    public IEnumerable<Type> IterateBusinessTypes(IEnumerable<Type> types)
    {
        if (_registrar == null || _services == null)
        {
            foreach (var type in types)
            {
                yield return type;
            }
            yield break;
        }
        
        foreach (var type in types)
        {
            _registrar.AddType(_services, type);
            yield return type;
        }
    }
}

/// <summary>
/// Configures the Monica dependency-injection module.
/// </summary>
public class ModuleDependencyInjectionGuide : ModuleGuide<ModuleDependencyInjection, ModuleDependencyInjectionOption,
    ModuleDependencyInjectionGuide>
{
    /// <summary>
    /// Enables or disables Monica automatic-registration diagnostics.
    /// </summary>
    /// <param name="enabled">
    /// <see langword="true"/> to capture the diagnostics snapshot and emit startup logs for automatic registration;
    /// otherwise, <see langword="false"/>.
    /// </param>
    /// <returns>The current guide instance.</returns>
    public ModuleDependencyInjectionGuide EnableAutoRegistrationDiagnostics(bool enabled = true)
    {
        ConfigureModuleOption(option => option.EnableAutoRegistrationDiagnostics = enabled);
        return this;
    }

    /// <summary>
    /// Enables or disables the per-type automatic-registration startup logs.
    /// </summary>
    /// <param name="enabled">
    /// <see langword="true"/> to emit a log entry for every auto-registered type during startup;
    /// otherwise, <see langword="false"/>. Disabled by default to keep startup output quiet.
    /// </param>
    /// <returns>The current guide instance.</returns>
    public ModuleDependencyInjectionGuide EnableAutoRegistrationLogging(bool enabled = true)
    {
        ConfigureModuleOption(option => option.EnableAutoRegistrationLogging = enabled);
        return this;
    }
}

/// <summary>
/// Configures Monica conventional dependency registration behavior.
/// </summary>
public class ModuleDependencyInjectionOption : ModuleOptions<ModuleDependencyInjection>
{
    /// <summary>
    /// Gets or sets a value indicating whether Monica should capture automatic-registration diagnostics.
    /// </summary>
    /// <remarks>
    /// When enabled, Monica emits startup diagnostic logs for automatic service registration and captures the
    /// diagnostics snapshot consumed by the dependency-injection UI module.
    /// Leave this disabled when diagnostics are not needed so startup registration avoids the extra tracking overhead.
    /// </remarks>
    public bool EnableAutoRegistrationDiagnostics { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether Monica writes a startup log entry for each auto-registered type.
    /// </summary>
    /// <remarks>
    /// This only controls the per-type startup logs (concrete-only warnings, registration info, and failures).
    /// It is independent of <see cref="EnableAutoRegistrationDiagnostics"/>, which captures the snapshot consumed by
    /// the dependency-injection UI module. Disabled by default to keep startup output quiet even when the UI page is on.
    /// </remarks>
    public bool EnableAutoRegistrationLogging { get; set; }
}
