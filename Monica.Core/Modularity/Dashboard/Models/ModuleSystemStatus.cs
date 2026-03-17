namespace Monica.Core.Modularity.Dashboard.Models;

/// <summary>
/// Overall module system status.
/// </summary>
public class ModuleSystemStatus
{
    /// <summary>
    /// Indicates whether initialization has completed.
    /// </summary>
    public bool IsInitialized { get; set; }

    /// <summary>
    /// Total number of modules.
    /// </summary>
    public int TotalModules { get; set; }

    /// <summary>
    /// Number of enabled modules.
    /// </summary>
    public int EnabledModules { get; set; }

    /// <summary>
    /// Number of disabled modules.
    /// </summary>
    public int DisabledModules { get; set; }

    /// <summary>
    /// Number of modules with registration errors.
    /// </summary>
    public int ErrorModules { get; set; }

    /// <summary>
    /// Total system initialization time in milliseconds.
    /// </summary>
    public long TotalInitializationTimeMs { get; set; }

    /// <summary>
    /// System start time.
    /// </summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// System initialization completion time.
    /// </summary>
    public DateTime? CompletionTime { get; set; }

    /// <summary>
    /// Current module system state.
    /// </summary>
    public ModuleSystemState State { get; set; }

    /// <summary>
    /// Indicates whether circular dependencies exist.
    /// </summary>
    public bool HasCircularDependencies { get; set; }

    /// <summary>
    /// Indicates whether registration errors exist.
    /// </summary>
    public bool HasRegistrationErrors { get; set; }
}

/// <summary>
/// Module system states.
/// </summary>
public enum ModuleSystemState
{
    /// <summary>
    /// Not initialized.
    /// </summary>
    NotInitialized,

    /// <summary>
    /// Initializing.
    /// </summary>
    Initializing,

    /// <summary>
    /// Initialized.
    /// </summary>
    Initialized,

    /// <summary>
    /// Failed.
    /// </summary>
    Failed
} 
