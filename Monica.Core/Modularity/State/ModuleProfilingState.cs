using System.Diagnostics;
using Monica.Core.Modularity.Models;
using Monica.Core.Modularity.Services.Support;

namespace Monica.Core.Modularity.State;

/// <summary>
/// Stores module initialization profiling data for one Monica application instance.
/// </summary>
internal sealed class ModuleProfilingState
{
    /// <summary>
    /// Gets the stopwatch that measures the full module-system initialization lifecycle.
    /// </summary>
    public Stopwatch SystemStopwatch { get; } = new();

    /// <summary>
    /// Gets stopwatches keyed by module-system phase name.
    /// </summary>
    public Dictionary<string, Stopwatch> PhaseStopwatches { get; } = [];

    /// <summary>
    /// Gets phase names in first-observed initialization order.
    /// </summary>
    public List<string> PhaseInitializationOrder { get; } = [];

    /// <summary>
    /// Gets per-module profiling data keyed by module type.
    /// </summary>
    public Dictionary<Type, ModuleProfileInfo> ModuleProfiles { get; } = [];

    /// <summary>
    /// Gets or sets whether system-level module profiling is currently running.
    /// </summary>
    public bool IsStarted { get; set; }

    /// <summary>
    /// Clears profiling data.
    /// </summary>
    public void Clear()
    {
        SystemStopwatch.Reset();
        PhaseStopwatches.Clear();
        PhaseInitializationOrder.Clear();
        ModuleProfiles.Clear();
        IsStarted = false;
    }
}
