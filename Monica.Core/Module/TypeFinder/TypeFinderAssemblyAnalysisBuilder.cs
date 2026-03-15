using System.Reflection;

namespace Monica.Core.Module.TypeFinder;

internal sealed class TypeFinderAssemblyAnalysisBuilder(
    ModuleCoreOptionTypeFinder options,
    IReadOnlyCollection<Assembly> scanAssemblies,
    IReadOnlyDictionary<string, TypeFinderAssemblyLoadFailure> failedLoads)
{
    public TypeFinderAssemblyAnalysis Build()
    {
        var plan = new TypeFinderAssemblyPlanBuilder(options).Build();
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
                assembly == plan.EntryAssembly))
            .ToList();

        var scanInfos = scanAssemblies
            .OrderBy(static assembly => assembly.GetName().Name, StringComparer.OrdinalIgnoreCase)
            .Select(assembly => CreateAssemblyInfo(
                assembly,
                isInScanSet: true,
                isEntryAssembly: assembly == plan.EntryAssembly))
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
                LoadError = failedLoads.GetValueOrDefault(reference.Name ?? string.Empty)?.ErrorMessage
            })
            .ToList();

        var dependencyLibraries = plan.DependencyLibraries
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
                IsLoaded = loadedByName.ContainsKey(descriptor.AssemblyName.Name ?? string.Empty),
                IsInScanSet = scanAssemblyNames.Contains(descriptor.AssemblyName.Name ?? string.Empty),
                RuntimeDllPath = descriptor.RuntimeDllPath,
                RuntimeDllExists = descriptor.RuntimeDllExists,
                LoadError = failedLoads.GetValueOrDefault(descriptor.AssemblyName.Name ?? string.Empty)?.ErrorMessage
            })
            .ToList();

        return new TypeFinderAssemblyAnalysis
        {
            EntryAssembly = CreateAssemblyInfo(
                plan.EntryAssembly,
                scanAssemblyNames.Contains(plan.EntryAssembly.GetName().Name ?? string.Empty),
                isEntryAssembly: true),
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
