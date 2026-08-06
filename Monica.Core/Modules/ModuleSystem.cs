using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Diagnostics.Facades;
using Monica.Core.Modularity.Diagnostics.Services;
using Monica.Core.Modularity.Metrics;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleSystemBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Configures the module system diagnostics module.
        /// </summary>
        public ModuleRegistration<ModuleSystem, ModuleSystemOption> AddModuleSystem(
            Action<ModuleSystemOption>? action = null)
        {
            return builder.AddModule<ModuleSystem, ModuleSystemOption>(action);
        }
    }
}

/// <summary>
/// Module system diagnostics module.
/// Registers inspection services and the host-facing diagnostics facade.
/// </summary>
public class ModuleSystem : MonicaModule<ModuleSystemOption>
{
    /// <summary>
    /// Registers diagnostics services for the module system.
    /// </summary>
    public override void ConfigureServices(ModuleContext<ModuleSystemOption> context)
    {
        var services = context.Services;
        services.AddSingleton<IModuleSystemInspectionService, ModuleSystemInspectionService>();
        services.AddSingleton<ModuleDiagnosticsFacade>();
        services.TryAddSingleton<ModuleInitMetrics>();
        services.AddHostedService<ModuleInitMetricsActivationService>();
    }
}

/// <summary>
/// Options for the module system diagnostics module.
/// </summary>
public class ModuleSystemOption : ModuleOptions<ModuleSystem>
{
}
