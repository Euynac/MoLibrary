using Monica.Core.Modularity.Models;

namespace Monica.Core.Modularity.Diagnostics.Models;

/// <summary>
/// Module system performance information.
/// </summary>
public class ModuleSystemPerformance
{
    /// <summary>
    /// Total system initialization time in milliseconds.
    /// </summary>
    public long TotalSystemInitializationTimeMs { get; set; }

    /// <summary>
    /// Performance for each system phase.
    /// </summary>
    public List<PhasePerformanceInfo> PhasePerformances { get; set; } = [];

    /// <summary>
    /// Performance for each module.
    /// </summary>
    public List<ModulePerformanceInfo> ModulePerformances { get; set; } = [];

    /// <summary>
    /// Five modules with the highest aggregate serial callback duration.
    /// </summary>
    public List<ModulePerformanceInfo> SlowestModules { get; set; } = [];

    /// <summary>
    /// Statistics for each configuration phase.
    /// </summary>
    public List<ConfigMethodStatistics> ConfigMethodStatistics { get; set; } = [];

    /// <summary>
    /// Total duration of all system phases, in milliseconds.
    /// </summary>
    public long TotalSystemPhaseDurationMs { get; set; }

    /// <summary>
    /// Total duration of all module phases, in milliseconds.
    /// </summary>
    public long TotalModulePhaseDurationMs { get; set; }

    /// <summary>
    /// Wall-clock span from the first composition work item starting until the last item completed.
    /// </summary>
    public long CompositionWorkWallDurationMs { get; set; }

    /// <summary>
    /// Sum of active worker durations across all module composition work items.
    /// This value may exceed wall time because work items can overlap.
    /// </summary>
    public long TotalCompositionWorkExecutionDurationMs { get; set; }

    /// <summary>
    /// Sum of queue durations across all module composition work items.
    /// </summary>
    public long TotalCompositionWorkQueueDurationMs { get; set; }

    /// <summary>
    /// Sum of waits imposed on the serial composition pipeline at composition-work checkpoints.
    /// </summary>
    public long TotalCompositionCheckpointWaitDurationMs { get; set; }

    /// <summary>
    /// Number of scheduled module composition work items.
    /// </summary>
    public int CompositionWorkItemCount { get; set; }

    /// <summary>
    /// Wait performance for each composition-work checkpoint.
    /// </summary>
    public List<ModuleCompositionCheckpointPerformanceInfo> CompositionCheckpoints { get; set; } = [];

    /// <summary>
    /// Number of system phases.
    /// </summary>
    public int SystemPhaseCount { get; set; }

    /// <summary>
    /// Total number of module phase executions.
    /// </summary>
    public int TotalModulePhaseExecutions { get; set; }
}

/// <summary>
/// Performance information for a single phase.
/// </summary>
public class PhasePerformanceInfo
{
    /// <summary>
    /// Phase name.
    /// </summary>
    public string PhaseName { get; set; } = string.Empty;

    /// <summary>
    /// Phase duration in milliseconds.
    /// </summary>
    public long DurationMs { get; set; }

    /// <summary>
    /// Phase order during initialization.
    /// </summary>
    public int Order { get; set; }
}

/// <summary>
/// Performance information for a single module.
/// </summary>
public class ModulePerformanceInfo
{
    /// <summary>
    /// Module type name.
    /// </summary>
    public string ModuleTypeName { get; set; } = string.Empty;

    /// <summary>
    /// Module key.
    /// </summary>
    public ModuleKey? ModuleKey { get; set; }

    /// <summary>
    /// Total duration of this module's serial composition phase callbacks, in milliseconds.
    /// </summary>
    public long SerialPhaseDurationMs { get; set; }

    /// <summary>
    /// Duration for each configuration phase.
    /// </summary>
    public Dictionary<ModulePhase, long> PhaseDurations { get; set; } = [];

    /// <summary>
    /// Composition work scheduled by this module in stable submission order.
    /// </summary>
    public List<ModuleCompositionWorkPerformanceInfo> CompositionWorkItems { get; set; } = [];
}

/// <summary>
/// Statistics for a configuration phase across modules.
/// </summary>
public class ConfigMethodStatistics
{
    /// <summary>
    /// Configuration phase.
    /// </summary>
    public ModulePhase ConfigMethod { get; set; }

    /// <summary>
    /// Total duration in milliseconds.
    /// </summary>
    public long TotalDurationMs { get; set; }

    /// <summary>
    /// Average duration in milliseconds.
    /// </summary>
    public long AverageDurationMs { get; set; }

    /// <summary>
    /// Number of modules that executed this phase.
    /// </summary>
    public int ModuleCount { get; set; }

    /// <summary>
    /// Slowest module name.
    /// </summary>
    public string SlowestModuleName { get; set; } = string.Empty;

    /// <summary>
    /// Slowest module duration in milliseconds.
    /// </summary>
    public long SlowestModuleDurationMs { get; set; }
} 
