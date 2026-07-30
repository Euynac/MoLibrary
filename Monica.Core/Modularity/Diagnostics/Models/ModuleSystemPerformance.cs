namespace Monica.Core.Modularity.Diagnostics.Models;

/// <summary>
/// Describes one immutable performance snapshot for a completed Monica module composition.
/// </summary>
public sealed class ModuleSystemPerformance
{
    /// <summary>
    /// Gets the end-to-end composition timeline and its derived critical-path measurements.
    /// </summary>
    public ModuleCompositionPerformance Composition { get; init; } = new();

    /// <summary>
    /// Gets module-oriented projections in registration order.
    /// </summary>
    public IReadOnlyList<ModulePerformanceInfo> Modules { get; init; } = [];
}
