using Monica.Core.Modularity.Diagnostics.Models;

namespace Monica.UI.UIModuleSystem.Support;

/// <summary>Represents one validated, portable baseline comparison.</summary>
public sealed record ModuleDiagnosticsComparison
{
    /// <summary>Gets the imported baseline.</summary>
    public required ModuleDiagnosticsExport Baseline { get; init; }

    /// <summary>Gets the total composition timing delta, current minus baseline.</summary>
    public double TotalDurationDeltaMs { get; init; }

    /// <summary>Gets the type-discovery timing delta, current minus baseline.</summary>
    public double TypeDiscoveryDurationDeltaMs { get; init; }

    /// <summary>Gets portable module identities added since the baseline.</summary>
    public IReadOnlyList<string> AddedModules { get; init; } = [];

    /// <summary>Gets portable module identities absent from the current snapshot.</summary>
    public IReadOnlyList<string> RemovedModules { get; init; } = [];

    /// <summary>Gets direct edges added since the baseline.</summary>
    public IReadOnlyList<string> AddedEdges { get; init; } = [];

    /// <summary>Gets direct edges removed since the baseline.</summary>
    public IReadOnlyList<string> RemovedEdges { get; init; } = [];

    internal static ModuleDiagnosticsComparison Create(
        ModuleDiagnosticsSnapshot current,
        ModuleDiagnosticsExport baseline)
    {
        var currentModules = current.Modules
            .Select(static module => module.ModuleKey.Id)
            .ToHashSet(StringComparer.Ordinal);
        var baselineModules = baseline.Modules
            .Select(static module => module.ModuleId)
            .ToHashSet(StringComparer.Ordinal);
        var portableIds = current.Modules.ToDictionary(
            static module => module.ModuleKey,
            static module => module.ModuleKey.Id);
        var currentEdges = current.Topology.Edges
            .Where(edge => portableIds.ContainsKey(edge.SourceModule) && portableIds.ContainsKey(edge.TargetModule))
            .Select(edge => EdgeId(portableIds[edge.SourceModule], portableIds[edge.TargetModule]))
            .ToHashSet(StringComparer.Ordinal);
        var baselineEdges = baseline.Edges
            .Select(static edge => EdgeId(edge.SourceModuleId, edge.TargetModuleId))
            .ToHashSet(StringComparer.Ordinal);

        return new ModuleDiagnosticsComparison
        {
            Baseline = baseline,
            TotalDurationDeltaMs = current.Summary.TotalCompositionDurationMs - baseline.Summary.TotalCompositionDurationMs,
            TypeDiscoveryDurationDeltaMs = current.Summary.TypeDiscoveryDurationMs - baseline.Summary.TypeDiscoveryDurationMs,
            AddedModules = currentModules.Except(baselineModules).Order(StringComparer.Ordinal).ToArray(),
            RemovedModules = baselineModules.Except(currentModules).Order(StringComparer.Ordinal).ToArray(),
            AddedEdges = currentEdges.Except(baselineEdges).Order(StringComparer.Ordinal).ToArray(),
            RemovedEdges = baselineEdges.Except(currentEdges).Order(StringComparer.Ordinal).ToArray()
        };
    }

    private static string EdgeId(string source, string target) => $"{source} -> {target}";
}
