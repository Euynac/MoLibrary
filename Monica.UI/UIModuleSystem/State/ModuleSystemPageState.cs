using Monica.Core.Modularity.Dashboard.Models;
using Monica.Core.Modularity.TypeFinder;

namespace Monica.UI.UIModuleSystem.State;

/// <summary>
/// Owns the mutable state of the module system dashboard page.
/// </summary>
public sealed class ModuleSystemPageState
{
    private const int AssemblyAnalysisTabIndex = 3;

    /// <summary>
    /// Gets the current system status snapshot.
    /// </summary>
    public ModuleSystemStatus? SystemStatus { get; private set; }

    /// <summary>
    /// Gets the current system performance snapshot.
    /// </summary>
    public ModuleSystemPerformance? SystemPerformance { get; private set; }

    /// <summary>
    /// Gets the current health check snapshot.
    /// </summary>
    public ModuleSystemHealthCheck? HealthCheck { get; private set; }

    /// <summary>
    /// Gets the current registration snapshot.
    /// </summary>
    public ModuleRegistrationInfo? RegistrationInfo { get; private set; }

    /// <summary>
    /// Gets the current dependency graph snapshot.
    /// </summary>
    public ModuleDependencyGraph? DependencyGraph { get; private set; }

    /// <summary>
    /// Gets the expensive assembly analysis snapshot when it has been loaded.
    /// </summary>
    public TypeFinderAssemblyAnalysis? AssemblyAnalysis { get; private set; }

    /// <summary>
    /// Gets the selected module detail.
    /// </summary>
    public ModuleDetailInfo? SelectedModuleDetail { get; private set; }

    /// <summary>
    /// Gets whether the module detail dialog should be visible.
    /// </summary>
    public bool ShowModuleDetail { get; private set; }

    /// <summary>
    /// Gets the active tab index.
    /// </summary>
    public int ActiveTabIndex { get; private set; }

    /// <summary>
    /// Gets whether assembly analysis should be loaded for the current state.
    /// </summary>
    public bool ShouldLoadAssemblyAnalysis => ActiveTabIndex == AssemblyAnalysisTabIndex && AssemblyAnalysis is null;

    /// <summary>
    /// Applies a new dashboard snapshot and clears any stale assembly analysis data.
    /// </summary>
    public void ApplyDashboardSnapshot(ModuleSystemDashboardSnapshot snapshot)
    {
        SystemStatus = snapshot.SystemStatus;
        SystemPerformance = snapshot.SystemPerformance;
        HealthCheck = snapshot.HealthCheck;
        RegistrationInfo = snapshot.RegistrationInfo;
        DependencyGraph = snapshot.DependencyGraph;
        AssemblyAnalysis = null;
    }

    /// <summary>
    /// Updates the active tab index.
    /// </summary>
    public void SetActiveTab(int tabIndex)
    {
        ActiveTabIndex = tabIndex;
    }

    /// <summary>
    /// Stores the assembly analysis snapshot.
    /// </summary>
    public void SetAssemblyAnalysis(TypeFinderAssemblyAnalysis analysis)
    {
        AssemblyAnalysis = analysis;
    }

    /// <summary>
    /// Opens the module detail dialog for the requested detail object.
    /// </summary>
    public void ShowModuleDetailDialog(ModuleDetailInfo detail)
    {
        SelectedModuleDetail = detail;
        ShowModuleDetail = true;
    }

    /// <summary>
    /// Updates the module detail dialog visibility.
    /// </summary>
    public void SetModuleDetailVisibility(bool isVisible)
    {
        ShowModuleDetail = isVisible;
        if (!isVisible)
        {
            SelectedModuleDetail = null;
        }
    }
}
