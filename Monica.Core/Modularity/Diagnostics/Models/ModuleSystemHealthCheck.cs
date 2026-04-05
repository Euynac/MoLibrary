using Monica.Core.Modularity.Models;

namespace Monica.Core.Modularity.Diagnostics.Models;

/// <summary>
/// Module system health check result.
/// </summary>
public class ModuleSystemHealthCheck
{
    /// <summary>
    /// Overall health state.
    /// </summary>
    public HealthStatus OverallHealth { get; set; }

    /// <summary>
    /// Health check summary.
    /// </summary>
    public string HealthSummary { get; set; } = string.Empty;

    /// <summary>
    /// Time of the check.
    /// </summary>
    public DateTime CheckTime { get; set; } = DateTime.Now;

    /// <summary>
    /// Individual health check items.
    /// </summary>
    public List<HealthCheckItem> HealthCheckItems { get; set; } = [];

    /// <summary>
    /// Issues discovered during the health check.
    /// </summary>
    public List<HealthIssue> Issues { get; set; } = [];

    /// <summary>
    /// Recommended actions.
    /// </summary>
    public List<string> Recommendations { get; set; } = [];

    /// <summary>
    /// Performance metrics.
    /// </summary>
    public HealthPerformanceMetrics PerformanceMetrics { get; set; } = new();
}

/// <summary>
/// Individual health check item.
/// </summary>
public class HealthCheckItem
{
    /// <summary>
    /// Check item name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Check item description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Check status.
    /// </summary>
    public HealthStatus Status { get; set; }

    /// <summary>
    /// Detailed check result.
    /// </summary>
    public string Details { get; set; } = string.Empty;

    /// <summary>
    /// Execution time in milliseconds.
    /// </summary>
    public long ExecutionTimeMs { get; set; }

    /// <summary>
    /// Related module, when applicable.
    /// </summary>
    public ModuleKey? RelatedModule { get; set; }
}

/// <summary>
/// Health issue.
/// </summary>
public class HealthIssue
{
    /// <summary>
    /// Issue severity.
    /// </summary>
    public IssueSeverity Severity { get; set; }

    /// <summary>
    /// Issue title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Issue description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Affected module.
    /// </summary>
    public ModuleKey? AffectedModule { get; set; }

    /// <summary>
    /// Issue type.
    /// </summary>
    public IssueType IssueType { get; set; }

    /// <summary>
    /// Recommended action.
    /// </summary>
    public string? RecommendedAction { get; set; }

    /// <summary>
    /// Discovery time.
    /// </summary>
    public DateTime DiscoveredAt { get; set; } = DateTime.Now;
}

/// <summary>
/// Health-related performance metrics.
/// </summary>
public class HealthPerformanceMetrics
{
    /// <summary>
    /// Average module initialization time in milliseconds.
    /// </summary>
    public double AverageModuleInitTimeMs { get; set; }

    /// <summary>
    /// Slowest module initialization time in milliseconds.
    /// </summary>
    public long SlowestModuleInitTimeMs { get; set; }

    /// <summary>
    /// Slowest module name.
    /// </summary>
    public string? SlowestModuleName { get; set; }

    /// <summary>
    /// Total system initialization time in milliseconds.
    /// </summary>
    public long TotalSystemInitTimeMs { get; set; }

    /// <summary>
    /// Initialization efficiency score from 0 to 100.
    /// </summary>
    public int InitializationEfficiencyScore { get; set; }

    /// <summary>
    /// Memory usage, when available.
    /// </summary>
    public long? MemoryUsageBytes { get; set; }
}

/// <summary>
/// Health states.
/// </summary>
public enum HealthStatus
{
    /// <summary>
    /// Healthy.
    /// </summary>
    Healthy,

    /// <summary>
    /// Warning.
    /// </summary>
    Warning,

    /// <summary>
    /// Unhealthy.
    /// </summary>
    Unhealthy,

    /// <summary>
    /// Critical.
    /// </summary>
    Critical,

    /// <summary>
    /// Unknown.
    /// </summary>
    Unknown
}

/// <summary>
/// Issue severities.
/// </summary>
public enum IssueSeverity
{
    /// <summary>
    /// Informational.
    /// </summary>
    Information,

    /// <summary>
    /// Low.
    /// </summary>
    Low,

    /// <summary>
    /// Medium.
    /// </summary>
    Medium,

    /// <summary>
    /// High.
    /// </summary>
    High,

    /// <summary>
    /// Critical.
    /// </summary>
    Critical
}

/// <summary>
/// Issue categories.
/// </summary>
public enum IssueType
{
    /// <summary>
    /// Configuration issue.
    /// </summary>
    Configuration,

    /// <summary>
    /// Performance issue.
    /// </summary>
    Performance,

    /// <summary>
    /// Dependency issue.
    /// </summary>
    Dependency,

    /// <summary>
    /// Initialization issue.
    /// </summary>
    Initialization,

    /// <summary>
    /// Memory issue.
    /// </summary>
    Memory,

    /// <summary>
    /// Other issue.
    /// </summary>
    Other
} 
