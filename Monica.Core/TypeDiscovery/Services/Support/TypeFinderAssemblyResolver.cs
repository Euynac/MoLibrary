using System.Reflection;
using Microsoft.Extensions.DependencyModel;
using Microsoft.Extensions.Logging;
using Monica.Core.TypeDiscovery.Models;

namespace Monica.Core.TypeDiscovery.Services.Support;

internal sealed class TypeFinderAssemblyResolver(TypeFinderOptions options, ILogger? logger)
{
    public TypeFinderAssemblyScan Resolve()
    {
        var plan = new TypeFinderAssemblyPlanBuilder(options).Build();
        var loadedByName = AppDomain.CurrentDomain.GetAssemblies()
            .Where(static assembly => !string.IsNullOrWhiteSpace(assembly.GetName().Name))
            .GroupBy(static assembly => assembly.GetName().Name!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.OrdinalIgnoreCase);

        var scanSet = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);
        var failedLoads = new Dictionary<string, TypeFinderAssemblyLoadFailure>(StringComparer.OrdinalIgnoreCase);

        foreach (var assemblyName in plan.FinalAssemblyNames)
        {
            var simpleName = assemblyName.Name ?? assemblyName.FullName ?? string.Empty;
            if (string.IsNullOrWhiteSpace(simpleName))
            {
                continue;
            }

            if (loadedByName.TryGetValue(simpleName, out var loadedAssembly))
            {
                TypeFinderAssemblyScanSet.AddToScanSet(scanSet, loadedAssembly);
                continue;
            }

            try
            {
                var assembly = Assembly.Load(assemblyName);
                TypeFinderAssemblyScanSet.AddToScanSet(scanSet, assembly);
            }
            catch (Exception exception)
            {
                failedLoads[simpleName] = TypeFinderAssemblyLoadFailure.CreateResolutionFailure(assemblyName, exception);

                logger?.LogWarning(
                    exception,
                    "Type finder skipped assembly {AssemblyName} because it could not be loaded.",
                    simpleName);
            }
        }

        foreach (var assembly in options.AdditionalAssemblies)
        {
            TypeFinderAssemblyScanSet.AddToScanSet(scanSet, assembly);
        }

        return new TypeFinderAssemblyScan
        {
            Assemblies = scanSet.Values
                .OrderBy(static assembly => assembly.GetName().Name, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            FailedLoads = failedLoads
        };
    }
}

internal sealed class TypeFinderAssemblyPlanBuilder(TypeFinderOptions options)
{
    public TypeFinderAssemblyPlan Build()
    {
        var entryAssembly = Assembly.GetEntryAssembly()
            ?? throw new InvalidOperationException("Unable to resolve the entry assembly.");

        var dependencyContext = DependencyContext.Default ?? DependencyContext.Load(entryAssembly);
        var referencedAssemblies = entryAssembly.GetReferencedAssemblies()
            .Where(static reference => !string.IsNullOrWhiteSpace(reference.Name))
            .ToArray();

        var dependencyLibraries = BuildDependencyLibraries(dependencyContext);
        var candidates = new Dictionary<string, AssemblyName>(StringComparer.OrdinalIgnoreCase);
        RegisterAssemblyName(candidates, entryAssembly.GetName());

        foreach (var dependencyLibrary in dependencyLibraries)
        {
            RegisterAssemblyName(candidates, dependencyLibrary.AssemblyName);
        }

        foreach (var referencedAssembly in referencedAssemblies)
        {
            RegisterAssemblyName(candidates, referencedAssembly);
        }

        var finalAssemblyNames = new Dictionary<string, AssemblyName>(StringComparer.OrdinalIgnoreCase);
        if (options.UseDefaultProjectAssemblies)
        {
            RegisterAssemblyName(finalAssemblyNames, entryAssembly.GetName());

            foreach (var assemblyName in ResolveDefaultProjectAssemblies(
                entryAssembly,
                dependencyLibraries,
                referencedAssemblies))
            {
                RegisterAssemblyName(finalAssemblyNames, assemblyName);
            }
        }

        var addPatternMatches = options.AddPatterns
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(pattern => ApplyAddPattern(pattern, candidates, finalAssemblyNames))
            .ToList();

        var excludePatternMatches = options.ExcludePatterns
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(pattern => ApplyExcludePattern(pattern, finalAssemblyNames))
            .ToList();

        return new TypeFinderAssemblyPlan
        {
            EntryAssembly = entryAssembly,
            DependencyContext = dependencyContext,
            ReferencedAssemblies = referencedAssemblies,
            DependencyLibraries = dependencyLibraries,
            FinalAssemblyNames = finalAssemblyNames.Values
                .OrderBy(static assemblyName => assemblyName.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            AddPatternMatches = addPatternMatches,
            ExcludePatternMatches = excludePatternMatches
        };
    }

    private static TypeFinderPatternMatchInfo ApplyAddPattern(
        string pattern,
        IReadOnlyDictionary<string, AssemblyName> candidates,
        IDictionary<string, AssemblyName> finalAssemblyNames)
    {
        var matchedCandidates = candidates.Values
            .Where(candidate => MatchesPattern(candidate.Name, candidate.FullName, pattern))
            .OrderBy(candidate => candidate.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var affectedNames = new List<string>();
        foreach (var candidate in matchedCandidates)
        {
            var candidateName = candidate.Name ?? candidate.FullName ?? string.Empty;
            if (string.IsNullOrWhiteSpace(candidateName) || finalAssemblyNames.ContainsKey(candidateName))
            {
                continue;
            }

            finalAssemblyNames[candidateName] = candidate;
            affectedNames.Add(candidateName);
        }

        return new TypeFinderPatternMatchInfo
        {
            Pattern = pattern,
            MatchedAssemblyNames = matchedCandidates
                .Select(static candidate => candidate.Name ?? candidate.FullName ?? string.Empty)
                .ToArray(),
            AffectedAssemblyNames = affectedNames
        };
    }

    private static TypeFinderPatternMatchInfo ApplyExcludePattern(
        string pattern,
        IDictionary<string, AssemblyName> finalAssemblyNames)
    {
        var matchedNames = finalAssemblyNames.Keys
            .Where(name => MatchesPattern(name, finalAssemblyNames[name].FullName, pattern))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var matchedName in matchedNames)
        {
            finalAssemblyNames.Remove(matchedName);
        }

        return new TypeFinderPatternMatchInfo
        {
            Pattern = pattern,
            MatchedAssemblyNames = matchedNames,
            AffectedAssemblyNames = matchedNames
        };
    }

    private static IReadOnlyList<TypeFinderDependencyLibraryDescriptor> BuildDependencyLibraries(DependencyContext? dependencyContext)
    {
        if (dependencyContext == null)
        {
            return [];
        }

        return dependencyContext.RuntimeLibraries
            .SelectMany(library =>
            {
                var assemblyNames = library.GetDefaultAssemblyNames(dependencyContext).ToArray();
                if (assemblyNames.Length == 0)
                {
                    assemblyNames = [new AssemblyName(library.Name)];
                }

                return assemblyNames.Select(assemblyName => new TypeFinderDependencyLibraryDescriptor(
                    library.Name,
                    library.Type,
                    library.Version,
                    assemblyName,
                    library.Dependencies.Select(static dependency => dependency.Name).ToArray()));
            })
            .ToList();
    }

    internal static IReadOnlyList<AssemblyName> ResolveDefaultProjectAssemblies(
        Assembly entryAssembly,
        IReadOnlyList<TypeFinderDependencyLibraryDescriptor> dependencyLibraries,
        IReadOnlyList<AssemblyName> referencedAssemblies)
    {
        ArgumentNullException.ThrowIfNull(entryAssembly);
        ArgumentNullException.ThrowIfNull(dependencyLibraries);
        ArgumentNullException.ThrowIfNull(referencedAssemblies);

        var projectLibrariesByName = dependencyLibraries
            .Where(static library => library.IsProject)
            .GroupBy(static library => library.LibraryName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.ToArray(), StringComparer.OrdinalIgnoreCase);

        if (projectLibrariesByName.Count == 0)
        {
            return ResolveDirectReferencedAssemblies(referencedAssemblies);
        }

        var rootLibraryNames = ResolveRootProjectLibraryNames(
            entryAssembly,
            projectLibrariesByName,
            referencedAssemblies);

        if (rootLibraryNames.Count == 0)
        {
            return ResolveDirectProjectAssemblies(projectLibrariesByName, referencedAssemblies);
        }

        var resolvedAssemblies = new Dictionary<string, AssemblyName>(StringComparer.OrdinalIgnoreCase);
        var visitedLibraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pendingLibraries = new Queue<string>(rootLibraryNames);

        while (pendingLibraries.Count > 0)
        {
            var libraryName = pendingLibraries.Dequeue();
            if (!visitedLibraries.Add(libraryName)
                || !projectLibrariesByName.TryGetValue(libraryName, out var descriptors))
            {
                continue;
            }

            foreach (var descriptor in descriptors)
            {
                RegisterAssemblyName(resolvedAssemblies, descriptor.AssemblyName);
            }

            foreach (var dependencyLibraryName in descriptors
                         .SelectMany(static descriptor => descriptor.DependencyLibraryNames)
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (projectLibrariesByName.ContainsKey(dependencyLibraryName))
                {
                    pendingLibraries.Enqueue(dependencyLibraryName);
                }
            }
        }

        var entryAssemblyName = entryAssembly.GetName().Name ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(entryAssemblyName))
        {
            resolvedAssemblies.Remove(entryAssemblyName);
        }

        if (resolvedAssemblies.Count > 0)
        {
            return resolvedAssemblies.Values
                .OrderBy(static assemblyName => assemblyName.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        // Some dependency contexts expose project libraries but omit project-to-project edges.
        // Preserve the previous behavior by falling back to direct project references.
        return ResolveDirectProjectAssemblies(projectLibrariesByName, referencedAssemblies);
    }

    private static IReadOnlyCollection<string> ResolveRootProjectLibraryNames(
        Assembly entryAssembly,
        IReadOnlyDictionary<string, TypeFinderDependencyLibraryDescriptor[]> projectLibrariesByName,
        IReadOnlyList<AssemblyName> referencedAssemblies)
    {
        var entryAssemblyName = entryAssembly.GetName().Name ?? string.Empty;

        var entryLibraryNames = projectLibrariesByName.Values
            .Where(group => group.Any(descriptor =>
                string.Equals(descriptor.AssemblyName.Name, entryAssemblyName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(descriptor.LibraryName, entryAssemblyName, StringComparison.OrdinalIgnoreCase)))
            .Select(static group => group[0].LibraryName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (entryLibraryNames.Length > 0)
        {
            return entryLibraryNames;
        }

        var directReferencedAssemblyNames = referencedAssemblies
            .Select(static reference => reference.Name)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return projectLibrariesByName.Values
            .Where(group => group.Any(descriptor =>
                directReferencedAssemblyNames.Contains(descriptor.AssemblyName.Name ?? string.Empty)))
            .Select(static group => group[0].LibraryName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<AssemblyName> ResolveDirectProjectAssemblies(
        IReadOnlyDictionary<string, TypeFinderDependencyLibraryDescriptor[]> projectLibrariesByName,
        IReadOnlyList<AssemblyName> referencedAssemblies)
    {
        var projectAssembliesByAssemblyName = projectLibrariesByName.Values
            .SelectMany(static group => group)
            .Where(static descriptor => !string.IsNullOrWhiteSpace(descriptor.AssemblyName.Name))
            .GroupBy(static descriptor => descriptor.AssemblyName.Name!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.First().AssemblyName, StringComparer.OrdinalIgnoreCase);

        var directProjectAssemblies = new Dictionary<string, AssemblyName>(StringComparer.OrdinalIgnoreCase);

        foreach (var reference in referencedAssemblies)
        {
            var referenceName = reference.Name ?? string.Empty;
            if (!projectAssembliesByAssemblyName.TryGetValue(referenceName, out var projectAssemblyName))
            {
                continue;
            }

            RegisterAssemblyName(directProjectAssemblies, projectAssemblyName);
        }

        return directProjectAssemblies.Values
            .OrderBy(static assemblyName => assemblyName.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<AssemblyName> ResolveDirectReferencedAssemblies(
        IReadOnlyList<AssemblyName> referencedAssemblies)
    {
        return referencedAssemblies
            .OrderBy(static assemblyName => assemblyName.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool MatchesPattern(string? name, string? fullName, string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return false;
        }

        return (name?.Contains(pattern, StringComparison.OrdinalIgnoreCase) ?? false)
               || (fullName?.Contains(pattern, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private static void RegisterAssemblyName(IDictionary<string, AssemblyName> assemblies, AssemblyName assemblyName)
    {
        var simpleName = assemblyName.Name ?? assemblyName.FullName ?? string.Empty;
        if (string.IsNullOrWhiteSpace(simpleName) || assemblies.ContainsKey(simpleName))
        {
            return;
        }

        assemblies[simpleName] = assemblyName;
    }
}

internal sealed class TypeFinderAssemblyPlan
{
    public required Assembly EntryAssembly { get; init; }

    public required DependencyContext? DependencyContext { get; init; }

    public required IReadOnlyList<AssemblyName> ReferencedAssemblies { get; init; }

    public required IReadOnlyList<TypeFinderDependencyLibraryDescriptor> DependencyLibraries { get; init; }

    public required IReadOnlyList<AssemblyName> FinalAssemblyNames { get; init; }

    public required IReadOnlyList<TypeFinderPatternMatchInfo> AddPatternMatches { get; init; }

    public required IReadOnlyList<TypeFinderPatternMatchInfo> ExcludePatternMatches { get; init; }
}

internal sealed class TypeFinderDependencyLibraryDescriptor(
    string libraryName,
    string libraryType,
    string? libraryVersion,
    AssemblyName assemblyName,
    IReadOnlyList<string> dependencyLibraryNames)
{
    public string LibraryName { get; } = libraryName;

    public string LibraryType { get; } = libraryType;

    public string? LibraryVersion { get; } = libraryVersion;

    public bool IsProject => string.Equals(LibraryType, "project", StringComparison.OrdinalIgnoreCase);

    public AssemblyName AssemblyName { get; } = assemblyName;

    public IReadOnlyList<string> DependencyLibraryNames { get; } = dependencyLibraryNames;

    public string RuntimeDllPath => Path.Combine(AppContext.BaseDirectory, $"{AssemblyName.Name}.dll");

    public bool RuntimeDllExists => File.Exists(RuntimeDllPath);
}

internal enum TypeFinderAssemblyLoadFailureStage
{
    Resolution,
    TypeScan
}

internal sealed class TypeFinderAssemblyLoadFailure(
    string name,
    string fullName,
    string errorMessage,
    TypeFinderAssemblyLoadFailureStage stage)
{
    public string Name { get; } = name;

    public string FullName { get; } = fullName;

    public string ErrorMessage { get; } = errorMessage;

    public TypeFinderAssemblyLoadFailureStage Stage { get; } = stage;

    public static TypeFinderAssemblyLoadFailure CreateResolutionFailure(AssemblyName assemblyName, Exception exception)
    {
        var simpleName = assemblyName.Name ?? assemblyName.FullName ?? string.Empty;
        return new TypeFinderAssemblyLoadFailure(
            simpleName,
            assemblyName.FullName ?? simpleName,
            exception.Message,
            TypeFinderAssemblyLoadFailureStage.Resolution);
    }

    public static TypeFinderAssemblyLoadFailure? CreateTypeScanFailure(Assembly assembly, ReflectionTypeLoadException exception)
    {
        var messages = exception.LoaderExceptions
            .Where(loaderException => loaderException != null)
            .Select(loaderException => loaderException!.Message)
            .Distinct()
            .ToArray();

        if (messages.Length == 0)
        {
            return null;
        }

        var assemblyName = assembly.GetName();
        var simpleName = assemblyName.Name ?? assembly.FullName ?? string.Empty;
        return new TypeFinderAssemblyLoadFailure(
            simpleName,
            assembly.FullName ?? simpleName,
            string.Join("; ", messages),
            TypeFinderAssemblyLoadFailureStage.TypeScan);
    }
}

internal sealed class TypeFinderAssemblyScan
{
    public required IReadOnlyList<Assembly> Assemblies { get; init; }

    public required IReadOnlyDictionary<string, TypeFinderAssemblyLoadFailure> FailedLoads { get; init; }
}

internal static class TypeFinderAssemblyScanSet
{
    public static void AddToScanSet(IDictionary<string, Assembly> scanSet, Assembly assembly)
    {
        var assemblyName = assembly.GetName().Name ?? assembly.FullName ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(assemblyName))
        {
            scanSet[assemblyName] = assembly;
        }
    }
}
