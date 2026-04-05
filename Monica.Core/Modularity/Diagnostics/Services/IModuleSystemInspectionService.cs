using Monica.Core.Modularity.Diagnostics.Models;
using Monica.Core.Modularity.Models;
using Monica.Core.TypeDiscovery.Models;

namespace Monica.Core.Modularity.Diagnostics.Services;

/// <summary>
/// Service interface that exposes module system status, performance, and dependency information.
/// Intended for dashboards and operational monitoring.
/// </summary>
public interface IModuleSystemInspectionService
{
    /// <summary>
    /// Gets the overall module system status.
    /// </summary>
    /// <returns>The system status.</returns>
    ModuleSystemStatus GetSystemStatus();

    /// <summary>
    /// Gets module system performance information.
    /// </summary>
    /// <returns>The performance snapshot.</returns>
    ModuleSystemPerformance GetSystemPerformance();

    /// <summary>
    /// Gets registration and dependency information for all modules.
    /// </summary>
    /// <returns>The registration information.</returns>
    ModuleRegistrationOverview GetRegistrationInfo();

    /// <summary>
    /// Gets detailed information for a specific module.
    /// </summary>
    /// <param name="moduleType">The module type.</param>
    /// <returns>The module details, or `null` if the module does not exist.</returns>
    ModuleDetailInfo? GetModuleDetail(Type moduleType);

    /// <summary>
    /// Gets detailed information for a specific module.
    /// </summary>
    /// <param name="moduleKey">The module key.</param>
    /// <returns>The module details, or `null` if the module does not exist.</returns>
    ModuleDetailInfo? GetModuleDetail(ModuleKey moduleKey);

    /// <summary>
    /// Gets the module dependency graph.
    /// </summary>
    /// <returns>The dependency graph.</returns>
    ModuleDependencyGraph GetDependencyGraph();

    /// <summary>
    /// Gets the module system health check result.
    /// </summary>
    /// <returns>The health check result.</returns>
    ModuleSystemHealthCheck GetHealthCheck();

    /// <summary>
    /// Gets the current type-finder assembly analysis snapshot.
    /// </summary>
    TypeFinderAssemblyAnalysis GetAssemblyAnalysis();
}
