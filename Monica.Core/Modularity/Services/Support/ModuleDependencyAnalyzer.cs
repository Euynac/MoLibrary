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
    /// Calculates all direct and transitive hard dependencies of a module.
    /// </summary>
    public IReadOnlySet<ModuleKey> CalculateModuleDependencies(ModuleKey moduleKey)
    {
        var projection = GetProjection();
        if (!projection.DependenciesByModule.TryGetValue(moduleKey, out var directDependencies))
        {
            return FrozenSet<ModuleKey>.Empty;
        }

        var result = new HashSet<ModuleKey>();
        var pending = new Queue<ModuleKey>(directDependencies);
        while (pending.TryDequeue(out var dependency))
        {
            if (!result.Add(dependency)
                || !projection.DependenciesByModule.TryGetValue(dependency, out var nestedDependencies))
            {
                continue;
            }

            foreach (var nestedDependency in nestedDependencies)
            {
                pending.Enqueue(nestedDependency);
            }
        }

        result.Remove(moduleKey);
        return result.ToFrozenSet();
    }

    /// <summary>
    /// Creates a detached diagnostic graph containing every declared module and hard dependency.
    /// </summary>
    public DirectedGraph<ModuleKey> CalculateCompleteModuleDependencyGraph()
    {
        var projection = GetProjection();
        var graph = new DirectedGraph<ModuleKey>();
        foreach (var module in projection.TypesByKey.Keys)
        {
            graph.AddNode(module);
        }

        foreach (var (module, dependencies) in projection.DependenciesByModule)
        {
            var moduleType = projection.TypesByKey[module];
            if (!projection.Graph.IsActive(moduleType))
            {
                continue;
            }

            foreach (var dependency in dependencies)
            {
                if (projection.Graph.IsActive(projection.TypesByKey[dependency]))
                {
                    graph.AddEdge(module, dependency);
                }
            }
        }

        return graph;
    }

    /// <summary>
    /// Determines whether the declared hard-dependency projection contains a cycle.
    /// </summary>
    public bool HasCircularDependencies() => CalculateCompleteModuleDependencyGraph().HasCycles();

    /// <summary>
    /// Gets active modules in the dependency-first order produced by the canonical graph compiler.
    /// </summary>
    public IReadOnlyList<ModuleKey> GetModulesInDependencyOrder()
    {
        var projection = GetProjection();
        return Array.AsReadOnly(projection.Graph.ActiveModules
            .Select(type => projection.KeysByType[type])
            .ToArray());
    }

    /// <summary>
    /// Gets direct, transitive, and reverse dependency information for a module.
    /// </summary>
    public ModuleDependencyInfo GetModuleDependencyInfo(ModuleKey moduleKey)
    {
        var projection = GetProjection();
        var directDependencies = projection.DependenciesByModule.GetValueOrDefault(moduleKey)
                                 ?? FrozenSet<ModuleKey>.Empty;
        var dependedBy = projection.DependenciesByModule
            .Where(entry => entry.Value.Contains(moduleKey))
            .Select(static entry => entry.Key)
            .ToHashSet();
        var cyclePath = FindCycleInvolvingModule(moduleKey);

        return new ModuleDependencyInfo
        {
            Module = moduleKey,
            DirectDependencies = directDependencies.ToHashSet(),
            AllDependencies = CalculateModuleDependencies(moduleKey).ToHashSet(),
            DependedByModules = dependedBy,
            CyclePath = cyclePath.ToList(),
            IsPartOfCycle = cyclePath.Count != 0
        };
    }

    /// <summary>
    /// Finds one concrete dependency cycle that begins and ends at the requested module.
    /// </summary>
    public IReadOnlyList<ModuleKey> FindCycleInvolvingModule(ModuleKey moduleKey)
    {
        var projection = GetProjection();
        var dependencies = projection.DependenciesByModule;
        if (!projection.TypesByKey.TryGetValue(moduleKey, out var moduleType)
            || !projection.Graph.IsActive(moduleType))
        {
            return Array.Empty<ModuleKey>();
        }

        var path = new List<ModuleKey> { moduleKey };
        var inPath = new HashSet<ModuleKey> { moduleKey };
        var visited = new HashSet<ModuleKey> { moduleKey };
        return Visit(moduleKey) is { } cycle ? Array.AsReadOnly(cycle) : Array.Empty<ModuleKey>();

        ModuleKey[]? Visit(ModuleKey current)
        {
            foreach (var dependency in dependencies[current])
            {
                if (!projection.Graph.IsActive(projection.TypesByKey[dependency]))
                {
                    continue;
                }

                if (dependency == moduleKey)
                {
                    return [.. path, moduleKey];
                }

                if (!visited.Add(dependency) || !inPath.Add(dependency))
                {
                    continue;
                }

                path.Add(dependency);
                var cycle = Visit(dependency);
                if (cycle is not null)
                {
                    return cycle;
                }

                path.RemoveAt(path.Count - 1);
                inPath.Remove(dependency);
            }

            return null;
        }
    }

    /// <summary>
    /// Gets dependency information for every declared module.
    /// </summary>
    public IReadOnlyDictionary<ModuleKey, ModuleDependencyInfo> GetAllModuleDependencyInfo()
    {
        return GetProjection().TypesByKey.Keys.ToFrozenDictionary(
            static key => key,
            GetModuleDependencyInfo);
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

/// <summary>
/// Detached directed graph used by module-system diagnostic models.
/// </summary>
/// <typeparam name="T">The node type.</typeparam>
public sealed class DirectedGraph<T> where T : notnull
{
    private readonly Dictionary<T, HashSet<T>> _adjacencyList = [];

    /// <summary>
    /// Gets all graph nodes.
    /// </summary>
    public IEnumerable<T> Nodes => _adjacencyList.Keys;

    /// <summary>
    /// Gets all directed edges as source-target pairs.
    /// </summary>
    public IEnumerable<(T Source, T Target)> Edges
    {
        get
        {
            foreach (var (source, targets) in _adjacencyList)
            {
                foreach (var target in targets)
                {
                    yield return (source, target);
                }
            }
        }
    }

    /// <summary>
    /// Adds a node when it is not already present.
    /// </summary>
    public void AddNode(T node)
    {
        _adjacencyList.TryAdd(node, []);
    }

    /// <summary>
    /// Adds a directed edge and its endpoint nodes.
    /// </summary>
    public void AddEdge(T source, T target)
    {
        AddNode(source);
        AddNode(target);
        _adjacencyList[source].Add(target);
    }

    /// <summary>
    /// Determines whether this detached graph contains a cycle.
    /// </summary>
    public bool HasCycles()
    {
        var visited = new HashSet<T>();
        var active = new HashSet<T>();
        return _adjacencyList.Keys.Any(Visit);

        bool Visit(T node)
        {
            if (active.Contains(node))
            {
                return true;
            }

            if (!visited.Add(node))
            {
                return false;
            }

            active.Add(node);
            foreach (var target in _adjacencyList[node])
            {
                if (Visit(target))
                {
                    return true;
                }
            }

            active.Remove(node);
            return false;
        }
    }

    /// <summary>
    /// Returns a stable topological ordering or throws when the graph is cyclic.
    /// </summary>
    public List<T> TopologicalSort()
    {
        var indegrees = _adjacencyList.Keys.ToDictionary(static node => node, static _ => 0);
        var dependents = _adjacencyList.Keys.ToDictionary(static node => node, static _ => new List<T>());
        foreach (var (owner, dependencies) in _adjacencyList)
        {
            indegrees[owner] = dependencies.Count;
            foreach (var dependency in dependencies)
            {
                dependents[dependency].Add(owner);
            }
        }

        var ready = new Queue<T>(_adjacencyList.Keys.Where(node => indegrees[node] == 0));
        var result = new List<T>(_adjacencyList.Count);
        while (ready.TryDequeue(out var node))
        {
            result.Add(node);
            foreach (var dependent in dependents[node])
            {
                if (--indegrees[dependent] == 0)
                {
                    ready.Enqueue(dependent);
                }
            }
        }

        if (result.Count != _adjacencyList.Count)
        {
            throw new InvalidOperationException("The directed graph contains a cycle and cannot be topologically sorted.");
        }

        return result;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        var builder = new StringBuilder("Module Dependency Graph:").AppendLine();
        foreach (var (node, targets) in _adjacencyList.OrderBy(static entry => entry.Key.ToString()))
        {
            builder.Append(node).Append(" -> ");
            builder.AppendLine(targets.Count == 0
                ? "(no dependencies)"
                : string.Join(", ", targets.OrderBy(static target => target.ToString())));
        }

        return builder.ToString();
    }
}
