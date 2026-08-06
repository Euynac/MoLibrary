using System.Collections.Frozen;
using System.Text;
using Monica.Core.Modularity.Diagnostics.Models;
using Monica.Core.Modularity.Models;
using Monica.Core.Modularity.Models.Internal;

namespace Monica.Core.Modularity.Services.Support;

/// <summary>
/// Projects diagnostic views from the host's immutable, type-keyed module graph.
/// </summary>
/// <remarks>
/// This service never owns a second dependency graph. <see cref="ModuleKey"/> values are generated only at the
/// diagnostics boundary; CLR <see cref="Type"/> identity remains authoritative for composition.
/// </remarks>
public sealed class ModuleDependencyAnalyzer(MonicaApplication application)
{
    private readonly object _projectionGate = new();
    private CompiledModuleGraph? _projectedGraph;
    private DependencyProjection? _projection;

    /// <summary>
    /// Gets an immutable mapping from module types to diagnostic keys.
    /// </summary>
    public IReadOnlyDictionary<Type, ModuleKey> ModuleKeysByType => GetProjection().KeysByType;

    /// <summary>
    /// Gets an immutable reverse mapping from diagnostic keys to module types.
    /// </summary>
    public IReadOnlyDictionary<ModuleKey, Type> ModuleTypesByKey => GetProjection().TypesByKey;

    /// <summary>
    /// Gets the immutable hard-dependency graph projected to diagnostic keys.
    /// </summary>
    public IReadOnlyDictionary<ModuleKey, IReadOnlySet<ModuleKey>> DependenciesByModule =>
        GetProjection().DependenciesByModule;

    /// <summary>
    /// Creates diagnostic metadata for a concrete module type.
    /// </summary>
    /// <param name="moduleType">The concrete module strategy type.</param>
    /// <returns>A stable diagnostic key. The key is not used for graph identity.</returns>
    internal ModuleKey ResolveModuleKey(Type moduleType)
    {
        ArgumentNullException.ThrowIfNull(moduleType);
        return ModuleKey.FromModuleType(moduleType);
    }

    /// <summary>
    /// Clears cached projections when an isolated host fixture resets the canonical graph.
    /// </summary>
    internal void Clear()
    {
        lock (_projectionGate)
        {
            _projectedGraph = null;
            _projection = null;
        }
    }

    /// <summary>
    /// Gets active modules in the dependency-first order produced by the canonical graph compiler.
    /// </summary>
    internal IReadOnlyList<ModuleKey> GetModulesInDependencyOrder()
    {
        var projection = GetProjection();
        return Array.AsReadOnly(projection.Graph.ActiveModules
            .Select(type => projection.KeysByType[type])
            .ToArray());
    }

    /// <summary>
    /// Gets diagnostic keys and dependency-first execution orders by module type.
    /// </summary>
    public IReadOnlyDictionary<Type, (ModuleKey ModuleKey, int Order)> GetModuleRegistrationOrder()
    {
        var projection = GetProjection();
        return application.Modules.Registrations.ToFrozenDictionary(
            static entry => entry.Key,
            entry => (projection.KeysByType[entry.Key], entry.Value.Order));
    }

    /// <summary>
    /// Builds a human-readable summary of the immutable graph and measured serial composition time.
    /// </summary>
    public string GetModuleRegistrationSummary()
    {
        var projection = GetProjection();
        var snapshots = application.Modules.RuntimeSnapshots
            .OrderBy(static snapshot => snapshot.RegisterInfo.Order)
            .ToArray();
        var disabled = application.Modules.DisabledRegistrations;
        var builder = new StringBuilder();
        builder.AppendLine("Module Registration Summary:");
        builder.AppendLine("=====================================");

        if (snapshots.Length == 0)
        {
            builder.AppendLine("No enabled modules found.");
        }
        else
        {
            builder.AppendLine("Enabled Modules:");
            builder.AppendLine("----------------");
            foreach (var snapshot in snapshots)
            {
                AppendModule(
                    builder,
                    snapshot.ModuleType,
                    $"Order {snapshot.RegisterInfo.Order:D4}",
                    $"Serial Phase Duration: {snapshot.SerialPhaseDurationMs}ms");
            }
        }

        if (disabled.Count != 0)
        {
            builder.AppendLine();
            builder.AppendLine("Disabled Modules:");
            builder.AppendLine("-----------------");
            foreach (var registration in disabled)
            {
                AppendModule(
                    builder,
                    registration.ModuleType,
                    $"DISABLED: {registration.DisabledReason}",
                    trailingLine: null);
            }
        }

        builder.AppendLine();
        builder.AppendLine("Statistics:");
        builder.AppendLine("===========");
        builder.AppendLine($"  Total modules: {snapshots.Length + disabled.Count}");
        builder.AppendLine($"  Enabled modules: {snapshots.Length}");
        builder.AppendLine($"  Disabled modules: {disabled.Count}");
        builder.AppendLine($"  Total serial phase duration: {snapshots.Sum(static snapshot => snapshot.SerialPhaseDurationMs)}ms");

        var slowest = snapshots
            .Where(static snapshot => snapshot.SerialPhaseDurationMs > 0)
            .OrderByDescending(static snapshot => snapshot.SerialPhaseDurationMs)
            .Take(5)
            .ToArray();
        if (slowest.Length != 0)
        {
            builder.AppendLine("  Slowest modules:");
            foreach (var snapshot in slowest)
            {
                builder.AppendLine($"    {snapshot.ModuleKey}: {snapshot.SerialPhaseDurationMs}ms");
            }
        }

        return builder.ToString();

        void AppendModule(StringBuilder text, Type moduleType, string state, string? trailingLine)
        {
            var key = projection.KeysByType[moduleType];
            text.AppendLine($"{state}: {key} ({moduleType.Name})");
            if (projection.DependenciesByModule[key] is { Count: > 0 } dependencies)
            {
                text.AppendLine($"           Dependencies: {string.Join(", ", dependencies)}");
            }

            if (trailingLine is not null)
            {
                text.AppendLine($"           {trailingLine}");
            }
        }
    }

    private DependencyProjection GetProjection()
    {
        var graph = application.Modules.CompiledGraph;
        lock (_projectionGate)
        {
            if (ReferenceEquals(graph, _projectedGraph) && _projection is not null)
            {
                return _projection;
            }

            var keysByType = graph.Modules.ToFrozenDictionary(
                static type => type,
                ModuleKey.FromModuleType);
            var typesByKey = keysByType.ToFrozenDictionary(
                static entry => entry.Value,
                static entry => entry.Key);
            var dependenciesByModule = graph.HardDependencies.ToFrozenDictionary(
                entry => keysByType[entry.Key],
                entry => (IReadOnlySet<ModuleKey>)entry.Value
                    .Select(type => keysByType[type])
                    .ToFrozenSet());

            _projectedGraph = graph;
            _projection = new DependencyProjection(
                graph,
                keysByType,
                typesByKey,
                dependenciesByModule);
            return _projection;
        }
    }

    private sealed record DependencyProjection(
        CompiledModuleGraph Graph,
        IReadOnlyDictionary<Type, ModuleKey> KeysByType,
        IReadOnlyDictionary<ModuleKey, Type> TypesByKey,
        IReadOnlyDictionary<ModuleKey, IReadOnlySet<ModuleKey>> DependenciesByModule);
}
