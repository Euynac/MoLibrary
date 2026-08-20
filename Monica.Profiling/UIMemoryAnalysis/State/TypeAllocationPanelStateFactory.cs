using Monica.Profiling.TypeAllocation.Facades;

namespace Monica.Profiling.UIMemoryAnalysis.State;

/// <summary>
/// Creates component-owned allocation state so facade subscriptions end when the tab is removed.
/// </summary>
public sealed class TypeAllocationPanelStateFactory(TypeAllocationFacade typeAllocationFacade)
{
    /// <summary>
    /// Creates a fresh state instance for one rendered allocation panel.
    /// </summary>
    public TypeAllocationPanelState Create() => new(typeAllocationFacade);
}
