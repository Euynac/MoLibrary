using System.Reflection;
using Mapster;

namespace Monica.Core.ObjectMapping.Providers.Mapster;

/// <summary>
/// Collects and activates the Mapster profiles owned by one Monica host.
/// </summary>
internal sealed class MapsterProfileCatalog
{
    private readonly Dictionary<string, Type> _discoveredProfiles = new(StringComparer.Ordinal);

    /// <summary>
    /// Records a concrete Mapster profile discovered through Monica's business-type pipeline.
    /// </summary>
    /// <param name="type">The current business type.</param>
    public void Discover(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (!IsDiscoverableProfile(type))
        {
            return;
        }

        _discoveredProfiles.TryAdd(GetProfileKey(type), type);
    }

    /// <summary>
    /// Applies explicit profiles first, followed by automatically discovered profiles in dependency-first order.
    /// </summary>
    /// <param name="config">The host-owned Mapster configuration.</param>
    /// <param name="explicitProfileTypes">Profiles explicitly registered through the module guide.</param>
    /// <param name="scannedAssemblies">The assemblies participating in this host's business-type discovery.</param>
    public void ApplyProfiles(
        TypeAdapterConfig config,
        IEnumerable<Type> explicitProfileTypes,
        IEnumerable<Assembly> scannedAssemblies)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(explicitProfileTypes);
        ArgumentNullException.ThrowIfNull(scannedAssemblies);

        foreach (var profileType in GetOrderedProfileTypes(explicitProfileTypes, scannedAssemblies))
        {
            CreateProfile(profileType).Register(config);
        }
    }

    /// <summary>
    /// Creates the final profile sequence for the current host.
    /// </summary>
    internal IReadOnlyList<Type> GetOrderedProfileTypes(
        IEnumerable<Type> explicitProfileTypes,
        IEnumerable<Assembly> scannedAssemblies)
    {
        var orderedProfiles = new List<Type>();
        var addedProfileKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var profileType in explicitProfileTypes)
        {
            AddOnce(profileType, orderedProfiles, addedProfileKeys);
        }

        foreach (var profileType in OrderByAssemblyDependencies(_discoveredProfiles.Values, scannedAssemblies))
        {
            AddOnce(profileType, orderedProfiles, addedProfileKeys);
        }

        return orderedProfiles;
    }

    /// <summary>
    /// Orders profile types by their scanned assembly dependency graph and then by stable type identity.
    /// </summary>
    internal static IReadOnlyList<Type> OrderByAssemblyDependencies(
        IEnumerable<Type> profileTypes,
        IEnumerable<Assembly> scannedAssemblies)
    {
        ArgumentNullException.ThrowIfNull(profileTypes);
        ArgumentNullException.ThrowIfNull(scannedAssemblies);

        var profiles = profileTypes
            .DistinctBy(GetProfileKey)
            .ToArray();
        if (profiles.Length == 0)
        {
            return [];
        }

        var assembliesByIdentity = new Dictionary<string, Assembly>(StringComparer.Ordinal);
        foreach (var assembly in scannedAssemblies.Concat(profiles.Select(static profile => profile.Assembly)))
        {
            assembliesByIdentity.TryAdd(GetAssemblyIdentity(assembly), assembly);
        }

        var assemblyDependencies = CreateAssemblyDependencyGraph(assembliesByIdentity);
        var profileAssemblyDependencies = ProjectProfileDependencyGraph(
            assemblyDependencies,
            profiles.Select(static profile => GetAssemblyIdentity(profile.Assembly)));
        var assemblyOrder = OrderAssemblyGraph(profileAssemblyDependencies);
        var orderByAssemblyIdentity = assemblyOrder
            .Select((assemblyIdentity, index) => (assemblyIdentity, index))
            .ToDictionary(static item => item.assemblyIdentity, static item => item.index, StringComparer.Ordinal);

        return profiles
            .OrderBy(profile => orderByAssemblyIdentity[GetAssemblyIdentity(profile.Assembly)])
            .ThenBy(GetProfileKey, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyDictionary<string, IReadOnlyCollection<string>> CreateAssemblyDependencyGraph(
        IReadOnlyDictionary<string, Assembly> assembliesByIdentity)
    {
        var dependenciesByAssembly = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal);

        foreach (var (assemblyIdentity, assembly) in assembliesByIdentity)
        {
            var dependencies = new HashSet<string>(StringComparer.Ordinal);
            foreach (var referencedAssembly in assembly.GetReferencedAssemblies())
            {
                var referencedAssemblyIdentity = GetAssemblyIdentity(referencedAssembly);
                if (!assembliesByIdentity.TryGetValue(referencedAssemblyIdentity, out var dependencyAssembly))
                {
                    continue;
                }

                var dependencyIdentity = GetAssemblyIdentity(dependencyAssembly);
                dependencies.Add(dependencyIdentity);
            }

            dependenciesByAssembly.Add(
                assemblyIdentity,
                dependencies.Order(StringComparer.Ordinal).ToArray());
        }

        return dependenciesByAssembly;
    }

    /// <summary>
    /// Projects the scanned assembly graph onto profile-owning assemblies while retaining dependencies connected by
    /// any number of non-profile assemblies.
    /// </summary>
    internal static IReadOnlyDictionary<string, IReadOnlyCollection<string>> ProjectProfileDependencyGraph(
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> assemblyDependencies,
        IEnumerable<string> profileAssemblyIdentities)
    {
        ArgumentNullException.ThrowIfNull(assemblyDependencies);
        ArgumentNullException.ThrowIfNull(profileAssemblyIdentities);

        var profileAssemblies = profileAssemblyIdentities
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var profileAssemblySet = profileAssemblies.ToHashSet(StringComparer.Ordinal);
        var projectedDependencies = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal);

        foreach (var profileAssembly in profileAssemblies)
        {
            if (!assemblyDependencies.TryGetValue(profileAssembly, out var directDependencies))
            {
                throw new InvalidOperationException(
                    $"Profile assembly '{profileAssembly}' is absent from the scanned object-mapping assembly graph.");
            }

            var profileDependencies = new HashSet<string>(StringComparer.Ordinal);
            var visitedNonProfileAssemblies = new HashSet<string>(StringComparer.Ordinal);
            var pendingDependencies = new Stack<string>(directDependencies);

            while (pendingDependencies.TryPop(out var dependency))
            {
                if (!assemblyDependencies.TryGetValue(dependency, out var transitiveDependencies))
                {
                    throw new InvalidOperationException(
                        $"Assembly '{profileAssembly}' reaches '{dependency}', which is absent from the scanned object-mapping assembly graph.");
                }

                if (profileAssemblySet.Contains(dependency))
                {
                    // The dependency's own projected edges preserve the rest of the path without redundant edges.
                    profileDependencies.Add(dependency);
                    continue;
                }

                if (!visitedNonProfileAssemblies.Add(dependency))
                {
                    continue;
                }

                foreach (var transitiveDependency in transitiveDependencies)
                {
                    pendingDependencies.Push(transitiveDependency);
                }
            }

            projectedDependencies.Add(
                profileAssembly,
                profileDependencies.Order(StringComparer.Ordinal).ToArray());
        }

        return projectedDependencies;
    }

    /// <summary>
    /// Topologically orders profile assemblies and distinguishes actual cycle participants from blocked consumers.
    /// </summary>
    internal static IReadOnlyList<string> OrderAssemblyGraph(
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> dependenciesByAssembly)
    {
        ArgumentNullException.ThrowIfNull(dependenciesByAssembly);

        var normalizedDependencies = dependenciesByAssembly.ToDictionary(
            static entry => entry.Key,
            static entry => (IReadOnlyCollection<string>)entry.Value
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray(),
            StringComparer.Ordinal);
        var dependentsByAssembly = normalizedDependencies.Keys.ToDictionary(
            static assemblyIdentity => assemblyIdentity,
            static _ => new HashSet<string>(StringComparer.Ordinal),
            StringComparer.Ordinal);

        foreach (var (assemblyIdentity, dependencies) in normalizedDependencies)
        {
            foreach (var dependencyIdentity in dependencies)
            {
                if (!dependentsByAssembly.TryGetValue(dependencyIdentity, out var dependents))
                {
                    throw new InvalidOperationException(
                        $"Assembly '{assemblyIdentity}' depends on '{dependencyIdentity}', which is absent from the object-mapping assembly graph.");
                }

                dependents.Add(assemblyIdentity);
            }
        }

        var remainingDependencyCounts = normalizedDependencies.ToDictionary(
            static entry => entry.Key,
            static entry => entry.Value.Count,
            StringComparer.Ordinal);
        var readyAssemblies = new SortedSet<string>(
            remainingDependencyCounts
                .Where(static entry => entry.Value == 0)
                .Select(static entry => entry.Key),
            StringComparer.Ordinal);
        var orderedAssemblies = new List<string>(normalizedDependencies.Count);

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

        if (orderedAssemblies.Count != normalizedDependencies.Count)
        {
            var cycleParticipants = FindCycleParticipants(normalizedDependencies);
            var blockedAssemblies = remainingDependencyCounts
                .Where(static entry => entry.Value > 0)
                .Select(static entry => entry.Key)
                .Except(cycleParticipants, StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            var blockedAssemblyMessage = blockedAssemblies.Length > 0
                ? $" Blocked profile assemblies: {string.Join(", ", blockedAssemblies)}."
                : string.Empty;
            throw new InvalidOperationException(
                $"Object-mapping profile order cannot be resolved because the profile assembly dependency graph contains a cycle. " +
                $"Cycle participants: {string.Join(", ", cycleParticipants)}.{blockedAssemblyMessage}");
        }

        return orderedAssemblies;
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

    private static bool IsDiscoverableProfile(Type type)
    {
        return type is { IsClass: true, IsAbstract: false, ContainsGenericParameters: false } &&
               typeof(IRegister).IsAssignableFrom(type);
    }

    private static IRegister CreateProfile(Type profileType)
    {
        if (!IsDiscoverableProfile(profileType))
        {
            throw CreateInvalidProfileException(profileType);
        }

        try
        {
            return Activator.CreateInstance(profileType, nonPublic: true) as IRegister
                   ?? throw CreateInvalidProfileException(profileType);
        }
        catch (Exception exception) when (exception is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"Object-mapping profile '{profileType.FullName}' must have a parameterless constructor that Monica can activate.",
                exception);
        }
    }

    private static InvalidOperationException CreateInvalidProfileException(Type profileType)
    {
        return new InvalidOperationException(
            $"Object-mapping profile '{profileType.FullName}' must be a concrete, closed {nameof(IRegister)} type with a parameterless constructor.");
    }

    private static void AddOnce(
        Type profileType,
        ICollection<Type> orderedProfiles,
        ISet<string> addedProfileKeys)
    {
        if (addedProfileKeys.Add(GetProfileKey(profileType)))
        {
            orderedProfiles.Add(profileType);
        }
    }

    private static string GetProfileKey(Type profileType)
    {
        return profileType.AssemblyQualifiedName
               ?? throw new InvalidOperationException(
                   $"Object-mapping profile '{profileType.FullName}' does not have an assembly-qualified type name.");
    }

    private static string GetAssemblyIdentity(Assembly assembly)
    {
        return assembly.FullName
               ?? assembly.GetName().FullName
               ?? assembly.GetName().Name
               ?? throw new InvalidOperationException("A scanned object-mapping assembly does not have an identity.");
    }

    private static string GetAssemblyIdentity(AssemblyName assembly)
    {
        return assembly.FullName
               ?? assembly.Name
               ?? throw new InvalidOperationException("A referenced object-mapping assembly does not have an identity.");
    }
}
