using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Diagnostics.Facades;
using Monica.Core.Modularity.Diagnostics.Services;
using Monica.Core.Modularity.Metrics;
using Monica.Core.Modularity.Models;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleSystemBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Configures the module system diagnostics module.
        /// </summary>
        public ModuleSystemGuide AddModuleSystem(Action<ModuleSystemOption>? action = null)
        {
            return builder.AddModule<ModuleSystem, ModuleSystemOption, ModuleSystemGuide>(action);
        }
    }
}

/// <summary>
/// Module system diagnostics module.
/// Registers inspection services and the host-facing diagnostics facade.
/// </summary>
[ModuleKey(BuiltInModuleKey.ModuleSystem)]
public class ModuleSystem(ModuleSystemOption option)
    : ModuleBase<ModuleSystem, ModuleSystemOption, ModuleSystemGuide>(option)
{
    /// <summary>
    /// Registers diagnostics services for the module system.
    /// </summary>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IModuleSystemInspectionService, ModuleSystemInspectionService>();
        services.AddSingleton<ModuleDiagnosticsFacade>();
        services.TryAddSingleton<ModuleInitMetrics>();
        services.AddHostedService<ModuleInitMetricsActivationService>();
    }
}

/// <summary>
/// Fluent guide for the module system diagnostics module.
/// </summary>
public class ModuleSystemGuide : ModuleGuide<ModuleSystem, ModuleSystemOption, ModuleSystemGuide>
{
}

/// <summary>
/// Options for the module system diagnostics module.
/// </summary>
public class ModuleSystemOption : ModuleOptions<ModuleSystem>
{
}
