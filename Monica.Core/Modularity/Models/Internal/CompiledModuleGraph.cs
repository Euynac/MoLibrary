using System.Collections.Frozen;
using Monica.Core.Modularity.Exceptions;

namespace Monica.Core.Modularity.Models.Internal;

/// <summary>
/// Immutable, type-keyed module graph produced once before option finalization.
/// </summary>
internal sealed class CompiledModuleGraph
{
    internal CompiledModuleGraph(
        IReadOnlyList<Type> modules,
        IReadOnlyList<Type> activeModules,
        IReadOnlyDictionary<Type, IReadOnlySet<Type>> hardDependencies,
        IReadOnlyDictionary<Type, IReadOnlySet<Type>> optionalOrderings,
        IReadOnlyDictionary<Type, IReadOnlySet<Type>> orderingDependencies,
        IReadOnlyDictionary<Type, IReadOnlySet<Type>> hardDependents)
    {
        Modules = modules;
        ActiveModules = activeModules;
        HardDependencies = hardDependencies;
        OptionalOrderings = optionalOrderings;
        OrderingDependencies = orderingDependencies;
        HardDependents = hardDependents;
        ActiveModuleSet = activeModules.ToFrozenSet();
    }

    internal IReadOnlyList<Type> Modules { get; }

    internal IReadOnlyList<Type> ActiveModules { get; }

    internal IReadOnlySet<Type> ActiveModuleSet { get; }

    internal IReadOnlyDictionary<Type, IReadOnlySet<Type>> HardDependencies { get; }

    internal IReadOnlyDictionary<Type, IReadOnlySet<Type>> OptionalOrderings { get; }

    internal IReadOnlyDictionary<Type, IReadOnlySet<Type>> OrderingDependencies { get; }

    internal IReadOnlyDictionary<Type, IReadOnlySet<Type>> HardDependents { get; }

    internal bool IsActive(Type moduleType) => ActiveModuleSet.Contains(moduleType);
}

/// <summary>
/// Compiles declaration drafts into one stable graph and reports exact strongly connected cycle components.
/// </summary>
internal static class ModuleGraphCompiler
{
    internal static CompiledModuleGraph Compile(
        IReadOnlyDictionary<Type, ModuleRegistrationState> registrations,
        IReadOnlyDictionary<Type, HashSet<Type>> hardDependencies,
        IReadOnlyDictionary<Type, HashSet<Type>> optionalOrderings,
        IReadOnlyDictionary<Type, int> registrationOrdinals)
    {
        ArgumentNullException.ThrowIfNull(registrations);
        ArgumentNullException.ThrowIfNull(hardDependencies);
        ArgumentNullException.ThrowIfNull(optionalOrderings);
        ArgumentNullException.ThrowIfNull(registrationOrdinals);

        var modules = registrations.Keys
            .OrderBy(type => registrationOrdinals[type])
            .ToArray();
        var activeSet = registrations
            .Where(static entry => entry.Value.DisabledReason is null)
            .Select(static entry => entry.Key)
            .ToHashSet();

        var frozenHardDependencies = FreezeGraph(modules, hardDependencies, rejectUnregisteredTargets: true);
        var frozenOptionalOrderings = FreezeGraph(modules, optionalOrderings, rejectUnregisteredTargets: false);
        var activeOrderingDependencies = BuildActiveOrderingGraph(
            modules,
            activeSet,
            frozenHardDependencies,
            frozenOptionalOrderings);
        var activeModules = StableTopologicalSort(
            activeSet,
            activeOrderingDependencies,
            registrationOrdinals);
        var hardDependents = ReverseGraph(modules, frozenHardDependencies);

        return new CompiledModuleGraph(
            Array.AsReadOnly(modules),
            Array.AsReadOnly(activeModules),
            frozenHardDependencies,
            frozenOptionalOrderings,
            activeOrderingDependencies,
            hardDependents);
    }

    private static IReadOnlyDictionary<Type, IReadOnlySet<Type>> FreezeGraph(
        IReadOnlyList<Type> modules,
        IReadOnlyDictionary<Type, HashSet<Type>> source,
        bool rejectUnregisteredTargets)
    {
        var moduleSet = modules.ToHashSet();
        var result = new Dictionary<Type, IReadOnlySet<Type>>(modules.Count);
        foreach (var module in modules)
        {
            if (!source.TryGetValue(module, out var targets))
            {
                result.Add(module, FrozenSet<Type>.Empty);
                continue;
            }

            foreach (var target in targets)
            {
                if (rejectUnregisteredTargets && !moduleSet.Contains(target))
                {
                    throw new ModuleRegistrationException(
                        $"Module {module.Name} declares unregistered module {target.Name} as a hard dependency.");
                }
            }

            result.Add(
                module,
                (rejectUnregisteredTargets ? targets.Where(moduleSet.Contains) : targets).ToFrozenSet());
        }

        return result.ToFrozenDictionary();
    }

    private static IReadOnlyDictionary<Type, IReadOnlySet<Type>> BuildActiveOrderingGraph(
        IReadOnlyList<Type> modules,
        IReadOnlySet<Type> activeModules,
        IReadOnlyDictionary<Type, IReadOnlySet<Type>> hardDependencies,
        IReadOnlyDictionary<Type, IReadOnlySet<Type>> optionalOrderings)
    {
        var result = new Dictionary<Type, IReadOnlySet<Type>>(activeModules.Count);
        foreach (var module in modules.Where(activeModules.Contains))
        {
            var dependencies = new HashSet<Type>();
            foreach (var dependency in hardDependencies[module])
            {
                if (!activeModules.Contains(dependency))
                {
                    throw new ModuleRegistrationException(
                        $"Active module {module.Name} requires omitted module {dependency.Name}.");
                }

                dependencies.Add(dependency);
            }

            dependencies.UnionWith(optionalOrderings[module].Where(activeModules.Contains));
            result.Add(module, dependencies.ToFrozenSet());
        }

        return result.ToFrozenDictionary();
    }

    private static Type[] StableTopologicalSort(
        IReadOnlySet<Type> activeModules,
        IReadOnlyDictionary<Type, IReadOnlySet<Type>> dependencies,
        IReadOnlyDictionary<Type, int> registrationOrdinals)
    {
        var indegrees = activeModules.ToDictionary(static type => type, static _ => 0);
        var dependents = activeModules.ToDictionary(static type => type, static _ => new List<Type>());
        foreach (var (module, moduleDependencies) in dependencies)
        {
            indegrees[module] = moduleDependencies.Count;
            foreach (var dependency in moduleDependencies)
            {
                dependents[dependency].Add(module);
            }
        }

        var ready = new PriorityQueue<Type, int>();
        foreach (var module in activeModules.OrderBy(type => registrationOrdinals[type]))
        {
            if (indegrees[module] == 0)
            {
                ready.Enqueue(module, registrationOrdinals[module]);
            }
        }

        var ordered = new List<Type>(activeModules.Count);
        while (ready.TryDequeue(out var module, out _))
        {
            ordered.Add(module);
            foreach (var dependent in dependents[module].OrderBy(type => registrationOrdinals[type]))
            {
                if (--indegrees[dependent] == 0)
                {
                    ready.Enqueue(dependent, registrationOrdinals[dependent]);
                }
            }
        }

        if (ordered.Count != activeModules.Count)
        {
            ThrowExactCycleError(dependencies, registrationOrdinals);
        }

        return ordered.ToArray();
    }

    private static void ThrowExactCycleError(
        IReadOnlyDictionary<Type, IReadOnlySet<Type>> graph,
        IReadOnlyDictionary<Type, int> registrationOrdinals)
    {
        var components = FindStronglyConnectedComponents(graph, registrationOrdinals)
            .Where(component => component.Count > 1
                                || graph[component[0]].Contains(component[0]))
            .OrderBy(component => component.Min(type => registrationOrdinals[type]))
            .ToArray();
        var lines = new List<string> { "The module graph contains ordering cycles:" };
        foreach (var component in components)
        {
            var cycle = FindCyclePath(component, graph, registrationOrdinals);
            lines.Add($"- {string.Join(" -> ", cycle.Select(static type => type.Name))}");
        }

        throw new ModuleRegistrationException(string.Join(Environment.NewLine, lines));
    }

    private static IReadOnlyList<IReadOnlyList<Type>> FindStronglyConnectedComponents(
        IReadOnlyDictionary<Type, IReadOnlySet<Type>> graph,
        IReadOnlyDictionary<Type, int> registrationOrdinals)
    {
        var index = 0;
        var indexes = new Dictionary<Type, int>();
        var lowLinks = new Dictionary<Type, int>();
        var stack = new Stack<Type>();
        var onStack = new HashSet<Type>();
        var components = new List<IReadOnlyList<Type>>();

        foreach (var module in graph.Keys.OrderBy(type => registrationOrdinals[type]))
        {
            if (!indexes.ContainsKey(module))
            {
                Visit(module);
            }
        }

        return components;

        void Visit(Type module)
        {
            indexes[module] = index;
            lowLinks[module] = index;
            index++;
            stack.Push(module);
            onStack.Add(module);

            foreach (var dependency in graph[module].OrderBy(type => registrationOrdinals[type]))
            {
                if (!indexes.ContainsKey(dependency))
                {
                    Visit(dependency);
                    lowLinks[module] = Math.Min(lowLinks[module], lowLinks[dependency]);
                }
                else if (onStack.Contains(dependency))
                {
                    lowLinks[module] = Math.Min(lowLinks[module], indexes[dependency]);
                }
            }

            if (lowLinks[module] != indexes[module])
            {
                return;
            }

            var component = new List<Type>();
            Type member;
            do
            {
                member = stack.Pop();
                onStack.Remove(member);
                component.Add(member);
            }
            while (member != module);

            component.Sort((left, right) => registrationOrdinals[left].CompareTo(registrationOrdinals[right]));
            components.Add(component.AsReadOnly());
        }
    }

    private static IReadOnlyList<Type> FindCyclePath(
        IReadOnlyList<Type> component,
        IReadOnlyDictionary<Type, IReadOnlySet<Type>> graph,
        IReadOnlyDictionary<Type, int> registrationOrdinals)
    {
        var members = component.ToHashSet();
        var path = new List<Type>();
        var pathIndexes = new Dictionary<Type, int>();
        var visited = new HashSet<Type>();

        foreach (var start in component.OrderBy(type => registrationOrdinals[type]))
        {
            if (Visit(start, out var cycle))
            {
                return cycle;
            }
        }

        return component;

        bool Visit(Type module, out IReadOnlyList<Type> cycle)
        {
            visited.Add(module);
            pathIndexes[module] = path.Count;
            path.Add(module);

            foreach (var dependency in graph[module]
                         .Where(members.Contains)
                         .OrderBy(type => registrationOrdinals[type]))
            {
                if (pathIndexes.TryGetValue(dependency, out var cycleStart))
                {
                    cycle = path.Skip(cycleStart).Append(dependency).ToArray();
                    return true;
                }

                if (!visited.Contains(dependency) && Visit(dependency, out cycle))
                {
                    return true;
                }
            }

            path.RemoveAt(path.Count - 1);
            pathIndexes.Remove(module);
            cycle = Array.Empty<Type>();
            return false;
        }
    }

    private static IReadOnlyDictionary<Type, IReadOnlySet<Type>> ReverseGraph(
        IReadOnlyList<Type> modules,
        IReadOnlyDictionary<Type, IReadOnlySet<Type>> graph)
    {
        var reversed = modules.ToDictionary(static type => type, static _ => new HashSet<Type>());
        foreach (var (module, dependencies) in graph)
        {
            foreach (var dependency in dependencies)
            {
                reversed[dependency].Add(module);
            }
        }

        return reversed.ToFrozenDictionary(
            static entry => entry.Key,
            static entry => (IReadOnlySet<Type>)entry.Value.ToFrozenSet());
    }
}
