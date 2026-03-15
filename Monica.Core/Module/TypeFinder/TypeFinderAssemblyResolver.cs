using System.Reflection;
using Microsoft.Extensions.DependencyModel;

namespace Monica.Core.Module.TypeFinder;

internal sealed class TypeFinderAssemblyResolver(ModuleCoreOptionTypeFinder options)
{
    public TypeFinderAssemblyResolution Resolve()
    {
        var entryAssembly = Assembly.GetEntryAssembly()
            ?? throw new InvalidOperationException("Unable to resolve the entry assembly.");

        var dependencyContext = DependencyContext.Default ?? DependencyContext.Load(entryAssembly);
        var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies().ToList();
        var loadedByName = loadedAssemblies
            .Where(static assembly => !string.IsNullOrWhiteSpace(assembly.GetName().Name))
            .GroupBy(static assembly => assembly.GetName().Name!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.OrdinalIgnoreCase);

        var dependencyDescriptors = BuildDependencyDescriptors(dependencyContext, loadedByName);
        var referenceCandidates = entryAssembly.GetReferencedAssemblies()
            .Select(static reference => new AssemblyCandidate(
                reference.Name ?? reference.FullName ?? string.Empty,
                reference.FullName ?? reference.Name ?? string.Empty,
                reference.Version?.ToString(),
                null,
                () => SafeLoad(reference)))
            .Where(static candidate => !string.IsNullOrWhiteSpace(candidate.Name))
            .ToList();

        var candidates = new Dictionary<string, AssemblyCandidate>(StringComparer.OrdinalIgnoreCase);

        foreach (var assembly in loadedAssemblies)
        {
            RegisterCandidate(
                candidates,
                new AssemblyCandidate(
                    assembly.GetName().Name ?? assembly.FullName ?? string.Empty,
                    assembly.FullName ?? assembly.GetName().Name ?? string.Empty,
                    assembly.GetName().Version?.ToString(),
                    assembly,
                    () => assembly));
        }

        foreach (var descriptor in dependencyDescriptors)
        {
            RegisterCandidate(
                candidates,
                new AssemblyCandidate(
                    descriptor.AssemblyName.Name ?? descriptor.LibraryName,
                    descriptor.AssemblyName.FullName ?? descriptor.LibraryName,
                    descriptor.AssemblyName.Version?.ToString(),
                    descriptor.LoadedAssembly,
                    () => descriptor.ResolveAssembly()));
        }

        foreach (var candidate in referenceCandidates)
        {
            RegisterCandidate(candidates, candidate);
        }

        foreach (var assembly in options.AdditionalAssemblies)
        {
            RegisterCandidate(
                candidates,
                new AssemblyCandidate(
                    assembly.GetName().Name ?? assembly.FullName ?? string.Empty,
                    assembly.FullName ?? assembly.GetName().Name ?? string.Empty,
                    assembly.GetName().Version?.ToString(),
                    assembly,
                    () => assembly));
        }

        var scanSet = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);
        if (options.UseDefaultProjectAssemblies)
        {
            AddToScanSet(scanSet, entryAssembly);

            foreach (var descriptor in dependencyDescriptors.Where(static descriptor => descriptor.IsProject))
            {
                var assembly = descriptor.ResolveAssembly();
                if (assembly != null)
                {
                    AddToScanSet(scanSet, assembly);
                }
            }
        }

        foreach (var assembly in options.AdditionalAssemblies)
        {
            AddToScanSet(scanSet, assembly);
        }

        var addPatternMatches = options.AddPatterns
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(pattern => ApplyPattern(pattern, candidates.Values, scanSet))
            .ToList();

        var excludePatternMatches = options.ExcludePatterns
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(pattern => RemovePattern(pattern, scanSet))
            .ToList();

        var scanAssemblyNames = scanSet.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var loadedInfos = loadedAssemblies
            .OrderBy(static assembly => assembly.GetName().Name, StringComparer.OrdinalIgnoreCase)
            .Select(assembly => CreateAssemblyInfo(
                assembly,
                scanAssemblyNames.Contains(assembly.GetName().Name ?? string.Empty),
                assembly == entryAssembly))
            .ToList();

        var scanInfos = scanSet.Values
            .OrderBy(static assembly => assembly.GetName().Name, StringComparer.OrdinalIgnoreCase)
            .Select(assembly => CreateAssemblyInfo(
                assembly,
                isInScanSet: true,
                isEntryAssembly: assembly == entryAssembly))
            .ToList();

        var manualAssemblies = options.AdditionalAssemblies
            .OrderBy(static assembly => assembly.GetName().Name, StringComparer.OrdinalIgnoreCase)
            .Select(assembly => new TypeFinderConfiguredAssemblyInfo
            {
                Name = assembly.GetName().Name ?? assembly.FullName ?? string.Empty,
                FullName = assembly.FullName ?? assembly.GetName().Name ?? string.Empty,
                Location = TryGetAssemblyLocation(assembly),
                IsInScanSet = scanAssemblyNames.Contains(assembly.GetName().Name ?? string.Empty)
            })
            .ToList();

        var referencedAssemblies = entryAssembly.GetReferencedAssemblies()
            .OrderBy(static reference => reference.Name, StringComparer.OrdinalIgnoreCase)
            .Select(reference => new TypeFinderReferencedAssemblyInfo
            {
                Name = reference.Name ?? reference.FullName ?? string.Empty,
                FullName = reference.FullName ?? reference.Name ?? string.Empty,
                Version = reference.Version?.ToString(),
                IsLoaded = loadedByName.ContainsKey(reference.Name ?? string.Empty),
                IsInScanSet = scanAssemblyNames.Contains(reference.Name ?? string.Empty)
            })
            .ToList();

        var dependencyLibraries = dependencyDescriptors
            .OrderByDescending(static descriptor => descriptor.IsProject)
            .ThenBy(static descriptor => descriptor.LibraryName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static descriptor => descriptor.AssemblyName.Name, StringComparer.OrdinalIgnoreCase)
            .Select(descriptor => new TypeFinderDependencyLibraryInfo
            {
                LibraryName = descriptor.LibraryName,
                LibraryType = descriptor.LibraryType,
                IsProject = descriptor.IsProject,
                AssemblyName = descriptor.AssemblyName.Name ?? descriptor.LibraryName,
                AssemblyVersion = descriptor.AssemblyName.Version?.ToString(),
                IsLoaded = descriptor.LoadedAssembly != null,
                IsInScanSet = scanAssemblyNames.Contains(descriptor.AssemblyName.Name ?? string.Empty),
                RuntimeDllPath = descriptor.RuntimeDllPath,
                RuntimeDllExists = descriptor.RuntimeDllExists,
                LoadError = descriptor.LoadError
            })
            .ToList();

        return new TypeFinderAssemblyResolution
        {
            Assemblies = scanSet.Values.OrderBy(static assembly => assembly.GetName().Name, StringComparer.OrdinalIgnoreCase).ToList(),
            Analysis = new TypeFinderAssemblyAnalysis
            {
                EntryAssembly = CreateAssemblyInfo(entryAssembly, scanAssemblyNames.Contains(entryAssembly.GetName().Name ?? string.Empty), isEntryAssembly: true),
                Configuration = new TypeFinderConfigurationInfo
                {
                    UseDefaultProjectAssemblies = options.UseDefaultProjectAssemblies,
                    AddPatterns = options.AddPatterns.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
                    ExcludePatterns = options.ExcludePatterns.Order(StringComparer.OrdinalIgnoreCase).ToArray()
                },
                AddPatternMatches = addPatternMatches,
                ExcludePatternMatches = excludePatternMatches,
                ManuallyAddedAssemblies = manualAssemblies,
                ScanAssemblies = scanInfos,
                LoadedAssemblies = loadedInfos,
                ReferencedAssemblies = referencedAssemblies,
                DependencyLibraries = dependencyLibraries
            }
        };
    }

    private static TypeFinderAssemblyInfo CreateAssemblyInfo(Assembly assembly, bool isInScanSet, bool isEntryAssembly)
    {
        var assemblyName = assembly.GetName();
        return new TypeFinderAssemblyInfo
        {
            Name = assemblyName.Name ?? assembly.FullName ?? string.Empty,
            FullName = assembly.FullName ?? assemblyName.Name ?? string.Empty,
            Version = assemblyName.Version?.ToString(),
            Location = TryGetAssemblyLocation(assembly),
            IsLoaded = true,
            IsInScanSet = isInScanSet,
            IsEntryAssembly = isEntryAssembly,
            IsDynamic = assembly.IsDynamic
        };
    }

    private static IReadOnlyList<DependencyLibraryDescriptor> BuildDependencyDescriptors(
        DependencyContext? dependencyContext,
        IReadOnlyDictionary<string, Assembly> loadedByName)
    {
        if (dependencyContext == null)
        {
            return [];
        }

        return dependencyContext.RuntimeLibraries
            .SelectMany(library =>
            {
                var assemblies = library.GetDefaultAssemblyNames(dependencyContext).ToArray();
                if (assemblies.Length == 0)
                {
                    assemblies = [new AssemblyName(library.Name)];
                }

                return assemblies.Select(assemblyName => new DependencyLibraryDescriptor(
                    library.Name,
                    library.Type,
                    assemblyName,
                    loadedByName.GetValueOrDefault(assemblyName.Name ?? string.Empty),
                    BuildRuntimeDllPath(assemblyName)));
            })
            .ToList();
    }

    private static TypeFinderPatternMatchInfo ApplyPattern(
        string pattern,
        IEnumerable<AssemblyCandidate> candidates,
        IDictionary<string, Assembly> scanSet)
    {
        var matchedCandidates = candidates
            .Where(candidate => MatchesPattern(candidate.Name, candidate.FullName, pattern))
            .OrderBy(candidate => candidate.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var affectedNames = new List<string>();
        foreach (var candidate in matchedCandidates)
        {
            var assembly = candidate.ResolveAssembly();
            if (assembly == null)
            {
                continue;
            }

            var assemblyName = assembly.GetName().Name ?? assembly.FullName ?? string.Empty;
            if (!scanSet.ContainsKey(assemblyName))
            {
                scanSet[assemblyName] = assembly;
                affectedNames.Add(assemblyName);
            }
        }

        return new TypeFinderPatternMatchInfo
        {
            Pattern = pattern,
            MatchedAssemblyNames = matchedCandidates.Select(static candidate => candidate.Name).ToArray(),
            AffectedAssemblyNames = affectedNames
        };
    }

    private static TypeFinderPatternMatchInfo RemovePattern(string pattern, IDictionary<string, Assembly> scanSet)
    {
        var matchedNames = scanSet.Keys
            .Where(name => MatchesPattern(name, scanSet[name].FullName ?? name, pattern))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var matchedName in matchedNames)
        {
            scanSet.Remove(matchedName);
        }

        return new TypeFinderPatternMatchInfo
        {
            Pattern = pattern,
            MatchedAssemblyNames = matchedNames,
            AffectedAssemblyNames = matchedNames
        };
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

    private static void RegisterCandidate(IDictionary<string, AssemblyCandidate> candidates, AssemblyCandidate candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate.Name))
        {
            return;
        }

        if (!candidates.ContainsKey(candidate.Name))
        {
            candidates[candidate.Name] = candidate;
        }
    }

    private static void AddToScanSet(IDictionary<string, Assembly> scanSet, Assembly assembly)
    {
        var assemblyName = assembly.GetName().Name ?? assembly.FullName ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(assemblyName))
        {
            scanSet[assemblyName] = assembly;
        }
    }

    private static Assembly? SafeLoad(AssemblyName assemblyName)
    {
        try
        {
            return Assembly.Load(assemblyName);
        }
        catch
        {
            return null;
        }
    }

    private static string BuildRuntimeDllPath(AssemblyName assemblyName)
    {
        return Path.Combine(AppContext.BaseDirectory, $"{assemblyName.Name}.dll");
    }

    private static string? TryGetAssemblyLocation(Assembly assembly)
    {
        try
        {
            return string.IsNullOrWhiteSpace(assembly.Location) ? null : assembly.Location;
        }
        catch
        {
            return null;
        }
    }

    private sealed class AssemblyCandidate(
        string name,
        string fullName,
        string? version,
        Assembly? assembly,
        Func<Assembly?> loader)
    {
        private Assembly? _resolvedAssembly = assembly;

        public string Name { get; } = name;
        public string FullName { get; } = fullName;
        public string? Version { get; } = version;

        public Assembly? ResolveAssembly()
        {
            _resolvedAssembly ??= loader();
            return _resolvedAssembly;
        }
    }

    private sealed class DependencyLibraryDescriptor(
        string libraryName,
        string libraryType,
        AssemblyName assemblyName,
        Assembly? loadedAssembly,
        string runtimeDllPath)
    {
        private Assembly? _resolvedAssembly = loadedAssembly;

        public string LibraryName { get; } = libraryName;
        public string LibraryType { get; } = libraryType;
        public bool IsProject => string.Equals(LibraryType, "project", StringComparison.OrdinalIgnoreCase);
        public AssemblyName AssemblyName { get; } = assemblyName;
        public Assembly? LoadedAssembly { get; } = loadedAssembly;
        public string RuntimeDllPath { get; } = runtimeDllPath;
        public bool RuntimeDllExists => File.Exists(RuntimeDllPath);
        public string? LoadError { get; private set; }

        public Assembly? ResolveAssembly()
        {
            if (_resolvedAssembly != null)
            {
                return _resolvedAssembly;
            }

            if (!RuntimeDllExists)
            {
                LoadError = $"Missing runtime DLL: {RuntimeDllPath}";
                return null;
            }

            try
            {
                _resolvedAssembly = Assembly.Load(AssemblyName);
                return _resolvedAssembly;
            }
            catch (Exception exception)
            {
                LoadError = exception.Message;
                return null;
            }
        }
    }
}

internal sealed class TypeFinderAssemblyResolution
{
    public required IReadOnlyList<Assembly> Assemblies { get; init; }

    public required TypeFinderAssemblyAnalysis Analysis { get; init; }
}
