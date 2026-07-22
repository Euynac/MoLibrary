using System.Reflection;

namespace Monica.Core.TypeDiscovery.Services.Support;

/// <summary>
/// Owns the immutable assembly dependency graph used by host-level type ordering.
/// </summary>
internal sealed class TypeDependencyGraph
{
    private readonly IReadOnlyDictionary<string, IReadOnlyCollection<string>> _dependenciesByAssembly;

    internal TypeDependencyGraph(
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> dependenciesByAssembly)
    {
        ArgumentNullException.ThrowIfNull(dependenciesByAssembly);

        _dependenciesByAssembly = dependenciesByAssembly.ToDictionary(
            static entry => entry.Key,
            static entry => (IReadOnlyCollection<string>)entry.Value
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray(),
            StringComparer.Ordinal);
    }

    /// <summary>
    /// Builds the graph once from the assemblies configured for the owning host.
    /// </summary>
    public static TypeDependencyGraph Create(IEnumerable<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        var assembliesByIdentity = new Dictionary<string, Assembly>(StringComparer.Ordinal);
        foreach (var assembly in assemblies)
        {
            ArgumentNullException.ThrowIfNull(assembly);
            assembliesByIdentity.TryAdd(GetAssemblyIdentity(assembly), assembly);
        }

        var dependenciesByAssembly = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal);
        foreach (var (assemblyIdentity, assembly) in assembliesByIdentity)
        {
            var dependencies = assembly.GetReferencedAssemblies()
                .Select(GetAssemblyIdentity)
                .Where(assembliesByIdentity.ContainsKey)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            dependenciesByAssembly.Add(assemblyIdentity, dependencies);
        }

        return new TypeDependencyGraph(dependenciesByAssembly);
    }

    /// <summary>
    /// Projects the complete graph onto selected assemblies and topologically orders that projection.
    /// Paths through any number of non-selected assemblies remain dependency edges between selected assemblies.
    /// </summary>
    public IReadOnlyList<string> OrderDependencyFirst(IEnumerable<string> selectedAssemblyIdentities)
    {
        ArgumentNullException.ThrowIfNull(selectedAssemblyIdentities);

        var selectedAssemblies = selectedAssemblyIdentities
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (selectedAssemblies.Length == 0)
        {
            return [];
        }

        var projectedDependencies = Project(selectedAssemblies);
        return TopologicallyOrder(projectedDependencies);
    }

    internal static string GetAssemblyIdentity(Assembly assembly)
    {
        return assembly.FullName
               ?? assembly.GetName().FullName
               ?? assembly.GetName().Name
               ?? throw new InvalidOperationException("A type-discovery assembly does not have an identity.");
    }

    private IReadOnlyDictionary<string, IReadOnlyCollection<string>> Project(
        IReadOnlyCollection<string> selectedAssemblies)
    {
        var selectedAssemblySet = selectedAssemblies.ToHashSet(StringComparer.Ordinal);
        var projectedDependencies = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal);

        foreach (var selectedAssembly in selectedAssemblies)
        {
            if (!_dependenciesByAssembly.TryGetValue(selectedAssembly, out var directDependencies))
            {
                throw new InvalidOperationException(
                    $"Selected assembly '{selectedAssembly}' is absent from the configured type-discovery assembly graph.");
            }

            var selectedDependencies = new HashSet<string>(StringComparer.Ordinal);
            var visitedNonSelectedAssemblies = new HashSet<string>(StringComparer.Ordinal);
            var pendingDependencies = new Stack<string>(directDependencies);

            while (pendingDependencies.TryPop(out var dependency))
            {
                if (!_dependenciesByAssembly.TryGetValue(dependency, out var transitiveDependencies))
                {
                    throw new InvalidOperationException(
                        $"Selected assembly '{selectedAssembly}' reaches '{dependency}', which is absent from the configured type-discovery assembly graph.");
                }

                if (selectedAssemblySet.Contains(dependency))
                {
                    // The selected dependency's projected edges preserve the remainder of the path.
                    selectedDependencies.Add(dependency);
                    continue;
                }

                if (!visitedNonSelectedAssemblies.Add(dependency))
                {
                    continue;
                }

                foreach (var transitiveDependency in transitiveDependencies)
                {
                    pendingDependencies.Push(transitiveDependency);
                }
            }

            projectedDependencies.Add(
                selectedAssembly,
                selectedDependencies.Order(StringComparer.Ordinal).ToArray());
        }

        return projectedDependencies;
    }

    private static IReadOnlyList<string> TopologicallyOrder(
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> dependenciesByAssembly)
    {
        var dependentsByAssembly = dependenciesByAssembly.Keys.ToDictionary(
            static assemblyIdentity => assemblyIdentity,
            static _ => new HashSet<string>(StringComparer.Ordinal),
            StringComparer.Ordinal);

        foreach (var (assemblyIdentity, dependencies) in dependenciesByAssembly)
        {
            foreach (var dependencyIdentity in dependencies)
            {
                if (!dependentsByAssembly.TryGetValue(dependencyIdentity, out var dependents))
                {
                    throw new InvalidOperationException(
                        $"Selected assembly '{assemblyIdentity}' depends on '{dependencyIdentity}', which is absent from the selected assembly graph.");
                }

                dependents.Add(assemblyIdentity);
            }
        }

        var remainingDependencyCounts = dependenciesByAssembly.ToDictionary(
            static entry => entry.Key,
            static entry => entry.Value.Count,
            StringComparer.Ordinal);
        var readyAssemblies = new SortedSet<string>(
            remainingDependencyCounts
                .Where(static entry => entry.Value == 0)
                .Select(static entry => entry.Key),
            StringComparer.Ordinal);
        var orderedAssemblies = new List<string>(dependenciesByAssembly.Count);

        while (readyAssemblies.Count > 0)
        {
            var assemblyIdentity = readyAssemblies.Min!;
            readyAssemblies.Remove(assemblyIdentity);
            orderedAssemblies.Add(assemblyIdentity);

            foreach (var dependentIdentity in dependentsByAssembly[assemblyIdentity])
            {
                remainingDependencyCounts[dependentIdentity]--;
                if (remainingDependencyCounts[dependentIdentity] == 0)
                {
                    readyAssemblies.Add(dependentIdentity);
                }
            }
        }

        if (orderedAssemblies.Count == dependenciesByAssembly.Count)
        {
            return orderedAssemblies;
        }

        var cycleParticipants = FindCycleParticipants(dependenciesByAssembly);
        var blockedAssemblies = remainingDependencyCounts
            .Where(static entry => entry.Value > 0)
            .Select(static entry => entry.Key)
            .Except(cycleParticipants, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var blockedAssemblyMessage = blockedAssemblies.Length > 0
            ? $" Blocked selected assemblies: {string.Join(", ", blockedAssemblies)}."
            : string.Empty;

        throw new InvalidOperationException(
            "Type dependency order cannot be resolved because the selected assembly dependency graph contains a cycle. " +
            $"Cycle participants: {string.Join(", ", cycleParticipants)}.{blockedAssemblyMessage}");
    }

    private static IReadOnlyList<string> FindCycleParticipants(
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> dependenciesByAssembly)
    {
        var nextIndex = 0;
        var indexes = new Dictionary<string, int>(StringComparer.Ordinal);
        var lowLinks = new Dictionary<string, int>(StringComparer.Ordinal);
        var stack = new Stack<string>();
        var assembliesOnStack = new HashSet<string>(StringComparer.Ordinal);
        var cycleParticipants = new HashSet<string>(StringComparer.Ordinal);

        foreach (var assembly in dependenciesByAssembly.Keys.Order(StringComparer.Ordinal))
        {
            if (!indexes.ContainsKey(assembly))
            {
                VisitStronglyConnectedComponent(assembly);
            }
        }

        return cycleParticipants.Order(StringComparer.Ordinal).ToArray();

        void VisitStronglyConnectedComponent(string assembly)
        {
            indexes[assembly] = nextIndex;
            lowLinks[assembly] = nextIndex;
            nextIndex++;
            stack.Push(assembly);
            assembliesOnStack.Add(assembly);

            foreach (var dependency in dependenciesByAssembly[assembly].Order(StringComparer.Ordinal))
            {
                if (!indexes.ContainsKey(dependency))
                {
                    VisitStronglyConnectedComponent(dependency);
                    lowLinks[assembly] = Math.Min(lowLinks[assembly], lowLinks[dependency]);
                }
                else if (assembliesOnStack.Contains(dependency))
                {
                    lowLinks[assembly] = Math.Min(lowLinks[assembly], indexes[dependency]);
                }
            }

            if (lowLinks[assembly] != indexes[assembly])
            {
                return;
            }

            var component = new List<string>();
            string componentAssembly;
            do
            {
                componentAssembly = stack.Pop();
                assembliesOnStack.Remove(componentAssembly);
                component.Add(componentAssembly);
            } while (!StringComparer.Ordinal.Equals(componentAssembly, assembly));

            var containsCycle = component.Count > 1 ||
                                dependenciesByAssembly[assembly]
                                    .Any(dependency => StringComparer.Ordinal.Equals(dependency, assembly));
            if (containsCycle)
            {
                cycleParticipants.UnionWith(component);
            }
        }
    }

    private static string GetAssemblyIdentity(AssemblyName assembly)
    {
        return assembly.FullName
               ?? assembly.Name
               ?? throw new InvalidOperationException("A referenced type-discovery assembly does not have an identity.");
    }
}
