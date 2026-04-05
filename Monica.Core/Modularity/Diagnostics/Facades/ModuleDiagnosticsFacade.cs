using Microsoft.Extensions.Logging;
using Monica.Core.Extensions;
using Monica.Core.Modularity.Diagnostics.Models;
using Monica.Core.Modularity.Diagnostics.Services;
using Monica.Core.Modularity.Models;
using Monica.Core.Results;
using Monica.Core.TypeDiscovery.Models;

namespace Monica.Core.Modularity.Diagnostics.Facades;

/// <summary>
/// Query-oriented facade for module system dashboard pages and other UI entry points.
/// </summary>
public sealed class ModuleDiagnosticsFacade(
    IModuleSystemInspectionService statusService,
    ILogger<ModuleDiagnosticsFacade> logger)
{
    /// <summary>
    /// Gets the main dashboard snapshot excluding the expensive assembly analysis section.
    /// </summary>
    public Task<Res<ModuleDiagnosticsSnapshot>> GetDashboardSnapshotAsync()
    {
        try
        {
            return Task.FromResult<Res<ModuleDiagnosticsSnapshot>>(Res.Ok(new ModuleDiagnosticsSnapshot
            {
                SystemStatus = statusService.GetSystemStatus(),
                SystemPerformance = statusService.GetSystemPerformance(),
                HealthCheck = statusService.GetHealthCheck(),
                RegistrationInfo = statusService.GetRegistrationInfo(),
                DependencyGraph = statusService.GetDependencyGraph()
            }));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to build the module system dashboard snapshot.");
            return Task.FromResult<Res<ModuleDiagnosticsSnapshot>>(Res.Fail(
                $"Failed to build the module system dashboard snapshot: {ex.GetMessageRecursively()}"));
        }
    }

    /// <summary>
    /// Gets the module assembly analysis snapshot.
    /// </summary>
    public Task<Res<TypeFinderAssemblyAnalysis>> GetAssemblyAnalysisAsync()
    {
        try
        {
            return Task.FromResult<Res<TypeFinderAssemblyAnalysis>>(Res.Ok(statusService.GetAssemblyAnalysis()));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load the module assembly analysis snapshot.");
            return Task.FromResult<Res<TypeFinderAssemblyAnalysis>>(Res.Fail(
                $"Failed to load the module assembly analysis snapshot: {ex.GetMessageRecursively()}"));
        }
    }

    /// <summary>
    /// Gets details for a single module.
    /// </summary>
    /// <param name="moduleKey">The module key to query.</param>
    public Task<Res<ModuleDetailInfo?>> GetModuleDetailAsync(ModuleKey moduleKey)
    {
        try
        {
            return Task.FromResult<Res<ModuleDetailInfo?>>(Res.Ok(statusService.GetModuleDetail(moduleKey)));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load module details for {ModuleKey}.", moduleKey);
            return Task.FromResult<Res<ModuleDetailInfo?>>(Res.Fail(
                $"Failed to load module details: {ex.GetMessageRecursively()}"));
        }
    }
}
