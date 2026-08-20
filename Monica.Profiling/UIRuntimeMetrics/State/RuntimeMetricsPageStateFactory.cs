using Microsoft.Extensions.Options;
using Monica.Modules;
using Monica.Profiling.RuntimeMetrics.Facades;

namespace Monica.Profiling.UIRuntimeMetrics.State;

/// <summary>
/// Creates page-owned runtime metrics state so navigation can deterministically stop its refresh loop.
/// </summary>
public sealed class RuntimeMetricsPageStateFactory(
    RuntimeMetricsFacade runtimeMetricsFacade,
    IOptions<ModuleRuntimeMetricsUIOption> options)
{
    /// <summary>
    /// Creates a fresh state instance for one rendered page.
    /// </summary>
    public RuntimeMetricsPageState Create() => new(runtimeMetricsFacade, options);
}
