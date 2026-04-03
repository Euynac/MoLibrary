namespace Monica.Core.Modularity.Dashboard.Models;

/// <summary>
/// Aggregated snapshot used by the module system dashboard page.
/// </summary>
public sealed class ModuleSystemDashboardSnapshot
{
    /// <summary>
    /// Gets or sets the current module system status summary.
    /// </summary>
    public required ModuleSystemStatus SystemStatus { get; init; }

    /// <summary>
    /// Gets or sets the module system performance snapshot.
    /// </summary>
    public required ModuleSystemPerformance SystemPerformance { get; init; }

    /// <summary>
    /// Gets or sets the latest module system health check result.
    /// </summary>
    public required ModuleSystemHealthCheck HealthCheck { get; init; }

    /// <summary>
    /// Gets or sets the module registration snapshot.
    /// </summary>
    public required ModuleRegistrationInfo RegistrationInfo { get; init; }

    /// <summary>
    /// Gets or sets the module dependency graph snapshot.
    /// </summary>
    public required ModuleDependencyGraph DependencyGraph { get; init; }
}
