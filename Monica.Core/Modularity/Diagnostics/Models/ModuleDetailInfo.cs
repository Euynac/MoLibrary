using Monica.Core.Modularity.Models;

namespace Monica.Core.Modularity.Diagnostics.Models;

/// <summary>
/// Detailed information about a single module.
/// </summary>
public class ModuleDetailInfo
{
    /// <summary>
    /// Basic module information.
    /// </summary>
    public ModuleBasicInfo BasicInfo { get; set; } = new();

    /// <summary>
    /// Module performance details.
    /// </summary>
    public ModulePerformanceInfo PerformanceInfo { get; set; } = new();

    /// <summary>
    /// Module dependency details.
    /// </summary>
    public ModuleDependencyInfo DependencyInfo { get; set; } = new();

    /// <summary>
    /// Module configuration details.
    /// </summary>
    public ModuleConfigInfo ConfigInfo { get; set; } = new();

    /// <summary>
    /// Module execution history.
    /// </summary>
    public List<ModulePhaseExecution> ExecutionHistory { get; set; } = [];

    /// <summary>
    /// Module errors, if any.
    /// </summary>
    public List<ModuleErrorInfo> Errors { get; set; } = [];
}

/// <summary>
/// Module configuration details.
/// </summary>
public class ModuleConfigInfo
{
    /// <summary>
    /// Indicates whether the module is disabled.
    /// </summary>
    public bool IsDisabled { get; set; }

    /// <summary>
    /// Indicates whether the module participates in the ASP.NET Core lifecycle.
    /// </summary>
    public bool IsWebModule { get; set; }

    /// <summary>
    /// Indicates whether the module is running in downgraded non-web mode.
    /// </summary>
    public bool IsDowngradedFromWebModule { get; set; }

    /// <summary>
    /// Reason the module was disabled.
    /// </summary>
    public string? DisabledReason { get; set; }

    /// <summary>
    /// Module configuration items.
    /// </summary>
    public Dictionary<string, object?> ConfigurationItems { get; set; } = [];

    /// <summary>
    /// Module options available for display in the UI.
    /// </summary>
    public List<ModuleConfiguredOption> ConfiguredOptions { get; set; } = [];

    /// <summary>
    /// Number of module registration requests.
    /// </summary>
    public int RegisterRequestCount { get; set; }

    /// <summary>
    /// Indicates whether the module participates in a circular dependency.
    /// </summary>
    public bool HasCircularDependency { get; set; }
}

/// <summary>
/// Represents a configured module option instance.
/// </summary>
public class ModuleConfiguredOption
{
    /// <summary>
    /// Gets or sets the option type.
    /// </summary>
    public Type OptionType { get; set; } = typeof(object);

    /// <summary>
    /// Gets or sets the configured option instance.
    /// </summary>
    public object? OptionInstance { get; set; }

    /// <summary>
    /// Gets or sets whether this option is an extra option.
    /// </summary>
    public bool IsExtraOption { get; set; }
}

/// <summary>
/// Execution information for a module phase.
/// </summary>
public class ModulePhaseExecution
{
    /// <summary>
    /// Executed phase.
    /// </summary>
    public ModulePhase Phase { get; set; }

    /// <summary>
    /// Start time.
    /// </summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// End time.
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Duration in milliseconds.
    /// </summary>
    public long DurationMs { get; set; }

    /// <summary>
    /// Execution status.
    /// </summary>
    public PhaseExecutionStatus Status { get; set; }

    /// <summary>
    /// Error message, if any.
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Module error information.
/// </summary>
public class ModuleErrorInfo
{
    /// <summary>
    /// Error type.
    /// </summary>
    public string ErrorType { get; set; } = string.Empty;

    /// <summary>
    /// Error message.
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// Phase where the error occurred.
    /// </summary>
    public ModulePhase? Phase { get; set; }

    /// <summary>
    /// Time when the error occurred.
    /// </summary>
    public DateTime ErrorTime { get; set; }

    /// <summary>
    /// Error stack trace.
    /// </summary>
    public string? StackTrace { get; set; }
}

/// <summary>
/// Phase execution status.
/// </summary>
public enum PhaseExecutionStatus
{
    /// <summary>
    /// Not started.
    /// </summary>
    NotStarted,

    /// <summary>
    /// Running.
    /// </summary>
    Running,

    /// <summary>
    /// Completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Failed.
    /// </summary>
    Failed,

    /// <summary>
    /// Skipped.
    /// </summary>
    Skipped
} 
