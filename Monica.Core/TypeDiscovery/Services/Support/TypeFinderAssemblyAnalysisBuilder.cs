using System.Reflection;
using Monica.Core.TypeDiscovery.Models;

namespace Monica.Core.TypeDiscovery.Services.Support;

internal sealed class TypeFinderAssemblyAnalysisBuilder(
    TypeFinderOptions options,
    IReadOnlyCollection<Assembly> scanAssemblies,
    IReadOnlyDictionary<string, TypeFinderAssemblyLoadFailure> failedLoads)
{
    public TypeFinderAssemblyAnalysis Build()
    {
        var plan = new TypeFinderAssemblyPlanBuilder(options).Build();
        var resolutionFailures = failedLoads.Values
            .Where(static failure => failure.Stage == TypeFinderAssemblyLoadFailureStage.Resolution)
            .ToDictionary(static failure => failure.Name, static failure => failure, StringComparer.OrdinalIgnoreCase);
        var typeScanFailures = failedLoads.Values
            .Where(static failure => failure.Stage == TypeFinderAssemblyLoadFailureStage.TypeScan)
            .ToDictionary(static failure => failure.Name, static failure => failure, StringComparer.OrdinalIgnoreCase);
        var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies().ToList();
        var loadedByName = loadedAssemblies
            .Where(static assembly => !string.IsNullOrWhiteSpace(assembly.GetName().Name))
            .GroupBy(static assembly => assembly.GetName().Name!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.OrdinalIgnoreCase);

        var scanAssemblyNames = scanAssemblies
            .Select(static assembly => assembly.GetName().Name)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var loadedInfos = loadedAssemblies
            .OrderBy(static assembly => assembly.GetName().Name, StringComparer.OrdinalIgnoreCase)
            .Select(assembly => CreateAssemblyInfo(
                assembly,
                scanAssemblyNames.Contains(assembly.GetName().Name ?? string.Empty),
                assembly == plan.EntryAssembly,
                GetLoadError(assembly.GetName().Name, typeScanFailures)))
            .ToList();

        var scanInfos = scanAssemblies
            .OrderBy(static assembly => assembly.GetName().Name, StringComparer.OrdinalIgnoreCase)
            .Select(assembly => CreateAssemblyInfo(
                assembly,
                isInScanSet: true,
                isEntryAssembly: assembly == plan.EntryAssembly,
                loadError: GetLoadError(assembly.GetName().Name, typeScanFailures)))
            .ToList();

        var manualAssemblies = options.AdditionalAssemblies
            .OrderBy(static assembly => assembly.GetName().Name, StringComparer.OrdinalIgnoreCase)
            .Select(assembly => new TypeFinderConfiguredAssemblyInfo
            {
                Name = assembly.GetName().Name ?? assembly.FullName ?? string.Empty,
                FullName = assembly.FullName ?? assembly.GetName().Name ?? string.Empty,
                Location = TryGetAssemblyLocation(assembly),
                IsInScanSet = true
            })
            .ToList();

        var referencedAssemblies = plan.ReferencedAssemblies
            .OrderBy(static reference => reference.Name, StringComparer.OrdinalIgnoreCase)
            .Select(reference => new TypeFinderReferencedAssemblyInfo
            {
                Name = reference.Name ?? reference.FullName ?? string.Empty,
                FullName = reference.FullName ?? reference.Name ?? string.Empty,
                Version = reference.Version?.ToString(),
                IsLoaded = loadedByName.ContainsKey(reference.Name ?? string.Empty),
                IsInScanSet = scanAssemblyNames.Contains(reference.Name ?? string.Empty),
                LoadError = GetLoadError(reference.Name, resolutionFailures)
            })
            .ToList();

        var dependencyLibraries = plan.DependencyLibraries
            .OrderByDescending(static descriptor => descriptor.IsProject)
            .ThenBy(static descriptor => descriptor.LibraryName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static descriptor => descriptor.AssemblyName.Name, StringComparer.OrdinalIgnoreCase)
            .Select(descriptor =>
            {
                var rawAssemblyVersion = ResolveRawDependencyAssemblyVersion(descriptor, loadedByName);

                return new TypeFinderDependencyLibraryInfo
                {
                    LibraryName = descriptor.LibraryName,
                    LibraryType = descriptor.LibraryType,
                    IsProject = descriptor.IsProject,
                    AssemblyName = descriptor.AssemblyName.Name ?? descriptor.LibraryName,
                    AssemblyVersion = ResolveDisplayedDependencyLibraryVersion(rawAssemblyVersion, descriptor),
                    RawAssemblyVersion = rawAssemblyVersion,
                    LibraryVersion = descriptor.LibraryVersion,
                    IsLoaded = loadedByName.ContainsKey(descriptor.AssemblyName.Name ?? string.Empty),
                    IsInScanSet = scanAssemblyNames.Contains(descriptor.AssemblyName.Name ?? string.Empty),
                    RuntimeDllPath = descriptor.RuntimeDllPath,
                    RuntimeDllExists = descriptor.RuntimeDllExists,
                    LoadError = GetLoadError(descriptor.AssemblyName.Name, resolutionFailures)
                };
            })
            .ToList();

        return new TypeFinderAssemblyAnalysis
        {
            EntryAssembly = CreateAssemblyInfo(
                plan.EntryAssembly,
                scanAssemblyNames.Contains(plan.EntryAssembly.GetName().Name ?? string.Empty),
                isEntryAssembly: true,
                loadError: GetLoadError(plan.EntryAssembly.GetName().Name, typeScanFailures)),
            Configuration = new TypeFinderConfigurationInfo
            {
                UseDefaultProjectAssemblies = options.UseDefaultProjectAssemblies,
                AddPatterns = options.AddPatterns.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
                ExcludePatterns = options.ExcludePatterns.Order(StringComparer.OrdinalIgnoreCase).ToArray()
            },
            AddPatternMatches = plan.AddPatternMatches,
            ExcludePatternMatches = plan.ExcludePatternMatches,
            ManuallyAddedAssemblies = manualAssemblies,
            ScanAssemblies = scanInfos,
            LoadedAssemblies = loadedInfos,
            ReferencedAssemblies = referencedAssemblies,
            DependencyLibraries = dependencyLibraries
        };
    }

    private static TypeFinderAssemblyInfo CreateAssemblyInfo(
        Assembly assembly,
        bool isInScanSet,
        bool isEntryAssembly,
        string? loadError)
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
            IsDynamic = assembly.IsDynamic,
            LoadError = loadError
        };
    }

    private static string? GetLoadError(
        string? assemblyName,
        IReadOnlyDictionary<string, TypeFinderAssemblyLoadFailure> failedLoads)
    {
        if (string.IsNullOrWhiteSpace(assemblyName))
        {
            return null;
        }

        return failedLoads.GetValueOrDefault(assemblyName)?.ErrorMessage;
    }

    private static string? ResolveRawDependencyAssemblyVersion(
        TypeFinderDependencyLibraryDescriptor descriptor,
        IReadOnlyDictionary<string, Assembly> loadedByName)
    {
        var assemblyName = descriptor.AssemblyName.Name ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(assemblyName)
            && loadedByName.TryGetValue(assemblyName, out var loadedAssembly))
        {
            return loadedAssembly.GetName().Version?.ToString()
                   ?? descriptor.AssemblyName.Version?.ToString();
        }

        return descriptor.AssemblyName.Version?.ToString();
    }

    private static string? ResolveDisplayedDependencyLibraryVersion(
        string? rawAssemblyVersion,
        TypeFinderDependencyLibraryDescriptor descriptor)
    {
        return rawAssemblyVersion
               ?? descriptor.LibraryVersion;
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
}
