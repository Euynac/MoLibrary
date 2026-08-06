using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
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
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Configures the memory diagnostics module.
        /// </summary>
        public ModuleRegistration<ModuleMemoryDiagnostics, ModuleMemoryDiagnosticsOption> AddMemoryDiagnostics(
            Action<ModuleMemoryDiagnosticsOption>? action = null)
        {
            return builder.AddModule<ModuleMemoryDiagnostics, ModuleMemoryDiagnosticsOption>(action);
        }
    }
}

/// <summary>
/// Memory diagnostics module.
/// </summary>
public class ModuleMemoryDiagnostics : MonicaModule<ModuleMemoryDiagnosticsOption>
{
    /// <inheritdoc />
    public override void ConfigureServices(ModuleContext<ModuleMemoryDiagnosticsOption> context)
    {
        var services = context.Services;
        services.AddSingleton<DotNetGcDumpProvider>();
        services.AddSingleton<MemoryDiagnosticsService>();
        services.AddScoped(sp => new MemoryDiagnosticsFacade(
            sp.GetRequiredService<MemoryDiagnosticsService>(),
            sp.GetRequiredService<ILogger<MemoryDiagnosticsFacade>>()));
    }
}

/// <summary>
/// Configuration options for the memory diagnostics module.
/// </summary>
public class ModuleMemoryDiagnosticsOption : ModuleOptions<ModuleMemoryDiagnostics>
{
}
