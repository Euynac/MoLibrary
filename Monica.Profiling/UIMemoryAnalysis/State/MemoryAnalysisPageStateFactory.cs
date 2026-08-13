using Microsoft.Extensions.Options;
using Monica.Modules;
using Monica.Profiling.MemoryDiagnostics.Facades;
using Monica.Profiling.RuntimeMetrics.Facades;

namespace Monica.Profiling.UIMemoryAnalysis.State;

/// <summary>
/// Creates page-owned memory analysis state so periodic work ends with the rendered page.
/// </summary>
public sealed class MemoryAnalysisPageStateFactory(
    MemoryDiagnosticsFacade memoryDiagnosticsFacade,
    RuntimeMetricsFacade runtimeMetricsFacade,
    IOptions<ModuleMemoryAnalysisUIOption> options)
{
    /// <summary>
    /// Creates a fresh state instance for one rendered page.
    /// </summary>
    public MemoryAnalysisPageState Create() => new(memoryDiagnosticsFacade, runtimeMetricsFacade, options);
}
