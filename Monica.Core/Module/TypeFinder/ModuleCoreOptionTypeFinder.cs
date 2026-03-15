using System.Reflection;

namespace Monica.Core.Module.TypeFinder;

/// <summary>
/// Configures how the domain type finder resolves assemblies to scan.
/// </summary>
public sealed class ModuleCoreOptionTypeFinder
{
    private readonly HashSet<string> _addPatterns = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _excludePatterns = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<Assembly> _additionalAssemblies = [];

    /// <summary>
    /// Gets a value indicating whether project assemblies from <see cref="Microsoft.Extensions.DependencyModel.DependencyContext.Default"/>
    /// are included automatically.
    /// </summary>
    public bool UseDefaultProjectAssemblies { get; private set; } = true;

    /// <summary>
    /// Gets fuzzy include patterns that can add assemblies by name.
    /// </summary>
    public IReadOnlyCollection<string> AddPatterns => _addPatterns;

    /// <summary>
    /// Gets fuzzy exclude patterns that can remove assemblies by name.
    /// </summary>
    public IReadOnlyCollection<string> ExcludePatterns => _excludePatterns;

    /// <summary>
    /// Gets exact assembly instances that must always be considered during scanning.
    /// </summary>
    public IReadOnlyCollection<Assembly> AdditionalAssemblies => _additionalAssemblies;

    /// <summary>
    /// Adds fuzzy assembly-name patterns.
    /// </summary>
    public ModuleCoreOptionTypeFinder Add(params string[] assemblyNamePatterns)
    {
        foreach (var pattern in assemblyNamePatterns.Where(static pattern => !string.IsNullOrWhiteSpace(pattern)))
        {
            _addPatterns.Add(pattern.Trim());
        }

        return this;
    }

    /// <summary>
    /// Adds exact assembly instances.
    /// </summary>
    public ModuleCoreOptionTypeFinder Add(params Assembly[] assemblies)
    {
        foreach (var assembly in assemblies.Where(static assembly => assembly != null))
        {
            _additionalAssemblies.Add(assembly);
        }

        return this;
    }

    /// <summary>
    /// Adds fuzzy exclude patterns.
    /// </summary>
    public ModuleCoreOptionTypeFinder Exclude(params string[] assemblyNamePatterns)
    {
        foreach (var pattern in assemblyNamePatterns.Where(static pattern => !string.IsNullOrWhiteSpace(pattern)))
        {
            _excludePatterns.Add(pattern.Trim());
        }

        return this;
    }

    /// <summary>
    /// Disables automatic inclusion of default project assemblies.
    /// </summary>
    public ModuleCoreOptionTypeFinder ExcludeDefault()
    {
        UseDefaultProjectAssemblies = false;
        return this;
    }
}
