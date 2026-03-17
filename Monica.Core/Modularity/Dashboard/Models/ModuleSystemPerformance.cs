using Monica.Core.Modularity.Models;

namespace Monica.Core.Modularity.Dashboard.Models;

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
    /// Five slowest modules.
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
    /// Total module duration in milliseconds.
    /// </summary>
    public long TotalDurationMs { get; set; }

    /// <summary>
    /// Duration for each configuration phase.
    /// </summary>
    public Dictionary<EMoModuleConfigMethods, long> PhaseDurations { get; set; } = [];
}

/// <summary>
/// Statistics for a configuration phase across modules.
/// </summary>
public class ConfigMethodStatistics
{
    /// <summary>
    /// Configuration phase.
    /// </summary>
    public EMoModuleConfigMethods ConfigMethod { get; set; }

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
