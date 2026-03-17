namespace Monica.Core.Modularity.TypeFinder;

/// <summary>
/// Represents a resolved snapshot of the type-finder assembly analysis state.
/// </summary>
public sealed class TypeFinderAssemblyAnalysis
{
    /// <summary>
    /// Gets the timestamp when the snapshot was generated.
    /// </summary>
    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the entry assembly information.
    /// </summary>
    public TypeFinderAssemblyInfo? EntryAssembly { get; init; }

    /// <summary>
    /// Gets the normalized type-finder configuration.
    /// </summary>
    public TypeFinderConfigurationInfo Configuration { get; init; } = new();

    /// <summary>
    /// Gets the results of fuzzy add-pattern evaluation.
    /// </summary>
    public IReadOnlyList<TypeFinderPatternMatchInfo> AddPatternMatches { get; init; } = [];

    /// <summary>
    /// Gets the results of fuzzy exclude-pattern evaluation.
    /// </summary>
    public IReadOnlyList<TypeFinderPatternMatchInfo> ExcludePatternMatches { get; init; } = [];

    /// <summary>
    /// Gets the exact assemblies that were added directly through the option object.
    /// </summary>
    public IReadOnlyList<TypeFinderConfiguredAssemblyInfo> ManuallyAddedAssemblies { get; init; } = [];

    /// <summary>
    /// Gets the final assembly set that will be scanned for types.
    /// </summary>
    public IReadOnlyList<TypeFinderAssemblyInfo> ScanAssemblies { get; init; } = [];

    /// <summary>
    /// Gets the assemblies currently loaded in the application domain.
    /// </summary>
    public IReadOnlyList<TypeFinderAssemblyInfo> LoadedAssemblies { get; init; } = [];

    /// <summary>
    /// Gets the static assembly references declared by the entry assembly.
    /// </summary>
    public IReadOnlyList<TypeFinderReferencedAssemblyInfo> ReferencedAssemblies { get; init; } = [];

    /// <summary>
    /// Gets dependency-manifest entries derived from the dependency context.
    /// </summary>
    public IReadOnlyList<TypeFinderDependencyLibraryInfo> DependencyLibraries { get; init; } = [];
}

/// <summary>
/// Represents normalized type-finder configuration values.
/// </summary>
public sealed class TypeFinderConfigurationInfo
{
    /// <summary>
    /// Gets a value indicating whether default project assemblies are included automatically.
    /// </summary>
    public bool UseDefaultProjectAssemblies { get; init; }

    /// <summary>
    /// Gets fuzzy add patterns.
    /// </summary>
    public IReadOnlyList<string> AddPatterns { get; init; } = [];

    /// <summary>
    /// Gets fuzzy exclude patterns.
    /// </summary>
    public IReadOnlyList<string> ExcludePatterns { get; init; } = [];
}

/// <summary>
/// Represents an assembly entry in the analysis snapshot.
/// </summary>
public sealed class TypeFinderAssemblyInfo
{
    /// <summary>
    /// Gets the simple assembly name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the full assembly name.
    /// </summary>
    public string FullName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the assembly version.
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// Gets the assembly location when available.
    /// </summary>
    public string? Location { get; init; }

    /// <summary>
    /// Gets a value indicating whether the assembly is already loaded.
    /// </summary>
    public bool IsLoaded { get; init; }

    /// <summary>
    /// Gets a value indicating whether the assembly belongs to the final scan set.
    /// </summary>
    public bool IsInScanSet { get; init; }

    /// <summary>
    /// Gets a value indicating whether the assembly is the entry assembly.
    /// </summary>
    public bool IsEntryAssembly { get; init; }

    /// <summary>
    /// Gets a value indicating whether the assembly is dynamic.
    /// </summary>
    public bool IsDynamic { get; init; }

    /// <summary>
    /// Gets the type-load error observed while scanning this assembly, when available.
    /// </summary>
    public string? LoadError { get; init; }
}

/// <summary>
/// Represents an exact assembly configured through the option object.
/// </summary>
public sealed class TypeFinderConfiguredAssemblyInfo
{
    /// <summary>
    /// Gets the simple assembly name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the full assembly name.
    /// </summary>
    public string FullName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the assembly location when available.
    /// </summary>
    public string? Location { get; init; }

    /// <summary>
    /// Gets a value indicating whether the assembly belongs to the final scan set.
    /// </summary>
    public bool IsInScanSet { get; init; }
}

/// <summary>
/// Represents how one fuzzy pattern affected assembly resolution.
/// </summary>
public sealed class TypeFinderPatternMatchInfo
{
    /// <summary>
    /// Gets the fuzzy pattern text.
    /// </summary>
    public string Pattern { get; init; } = string.Empty;

    /// <summary>
    /// Gets the assembly names matched by the pattern.
    /// </summary>
    public IReadOnlyList<string> MatchedAssemblyNames { get; init; } = [];

    /// <summary>
    /// Gets the matched assembly names that changed the final scan set.
    /// </summary>
    public IReadOnlyList<string> AffectedAssemblyNames { get; init; } = [];
}

/// <summary>
/// Represents a static assembly reference declared by the entry assembly.
/// </summary>
public sealed class TypeFinderReferencedAssemblyInfo
{
    /// <summary>
    /// Gets the simple assembly name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the full assembly name.
    /// </summary>
    public string FullName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the referenced assembly version.
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// Gets a value indicating whether the referenced assembly is already loaded.
    /// </summary>
    public bool IsLoaded { get; init; }

    /// <summary>
    /// Gets a value indicating whether the referenced assembly is part of the final scan set.
    /// </summary>
    public bool IsInScanSet { get; init; }

    /// <summary>
    /// Gets the load error observed while resolving the assembly, when available.
    /// </summary>
    public string? LoadError { get; init; }
}

/// <summary>
/// Represents one dependency-context library row in the analysis snapshot.
/// </summary>
public sealed class TypeFinderDependencyLibraryInfo
{
    /// <summary>
    /// Gets the dependency library name.
    /// </summary>
    public string LibraryName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the dependency library type.
    /// </summary>
    public string LibraryType { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the dependency library is a project reference.
    /// </summary>
    public bool IsProject { get; init; }

    /// <summary>
    /// Gets the assembly name contributed by the dependency library.
    /// </summary>
    public string AssemblyName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the visible dependency version shown to the user.
    /// </summary>
    public string? AssemblyVersion { get; init; }

    /// <summary>
    /// Gets the raw assembly version before any fallback to DependencyContext package metadata.
    /// </summary>
    public string? RawAssemblyVersion { get; init; }

    /// <summary>
    /// Gets the package or library version reported by DependencyContext.
    /// </summary>
    public string? LibraryVersion { get; init; }

    /// <summary>
    /// Gets a value indicating whether the assembly is already loaded.
    /// </summary>
    public bool IsLoaded { get; init; }

    /// <summary>
    /// Gets a value indicating whether the assembly belongs to the final scan set.
    /// </summary>
    public bool IsInScanSet { get; init; }

    /// <summary>
    /// Gets the expected runtime DLL path in the current application directory.
    /// </summary>
    public string RuntimeDllPath { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the runtime DLL exists in the current application directory.
    /// </summary>
    public bool RuntimeDllExists { get; init; }

    /// <summary>
    /// Gets a value indicating whether the assembly can be loaded directly from the runtime directory.
    /// </summary>
    public bool CanLoadFromRuntimeDirectory => IsLoaded || RuntimeDllExists;

    /// <summary>
    /// Gets the load error observed while resolving the assembly, when available.
    /// </summary>
    public string? LoadError { get; init; }
}
