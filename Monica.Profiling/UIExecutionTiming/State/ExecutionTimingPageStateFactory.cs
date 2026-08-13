using Microsoft.Extensions.Options;
using Monica.Modules;
using Monica.Profiling.ExecutionTiming.Facades;

namespace Monica.Profiling.UIExecutionTiming.State;

/// <summary>
/// Creates page-owned execution timing state so periodic work ends with the rendered page.
/// </summary>
public sealed class ExecutionTimingPageStateFactory(
    ExecutionTimingFacade executionTimingFacade,
    IOptions<ModuleExecutionTimingUIOption> options)
{
    /// <summary>
    /// Creates a fresh state instance for one rendered page.
    /// </summary>
    public ExecutionTimingPageState Create() => new(executionTimingFacade, options);
}
