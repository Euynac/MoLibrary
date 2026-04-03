using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.Profiling.MemoryDiagnostics.Facades;
using Monica.Profiling.MemoryDiagnostics.Providers.DotNetTools;
using Monica.Profiling.MemoryDiagnostics.Services;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

/// <summary>
/// Builder extensions for the memory diagnostics module.
/// </summary>
public static class ModuleMemoryDiagnosticsBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configures the memory diagnostics module.
        /// </summary>
        public static ModuleMemoryDiagnosticsGuide AddMemoryDiagnostics(Action<ModuleMemoryDiagnosticsOption>? action = null)
        {
            return new ModuleMemoryDiagnosticsGuide().Register(action);
        }
    }
}

/// <summary>
/// Memory diagnostics module.
/// </summary>
[ModuleKey(EMoModuleKey.MemoryDiagnostics)]
public class ModuleMemoryDiagnostics(ModuleMemoryDiagnosticsOption option)
    : MoModule<ModuleMemoryDiagnostics, ModuleMemoryDiagnosticsOption, ModuleMemoryDiagnosticsGuide>(option)
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<DotNetGcDumpProvider>();
        services.AddSingleton<MemoryDiagnosticsService>();
        services.AddScoped(sp => new MemoryDiagnosticsFacade(
            sp.GetRequiredService<MemoryDiagnosticsService>(),
            sp.GetRequiredService<ILogger<MemoryDiagnosticsFacade>>()));
    }
}

/// <summary>
/// Fluent guide for the memory diagnostics module.
/// </summary>
public class ModuleMemoryDiagnosticsGuide
    : MoModuleGuide<ModuleMemoryDiagnostics, ModuleMemoryDiagnosticsOption, ModuleMemoryDiagnosticsGuide>
{
}

/// <summary>
/// Configuration options for the memory diagnostics module.
/// </summary>
public class ModuleMemoryDiagnosticsOption : MoModuleOption<ModuleMemoryDiagnostics>
{
}
