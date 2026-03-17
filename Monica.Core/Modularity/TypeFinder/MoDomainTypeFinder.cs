using System.Reflection;
using Microsoft.Extensions.Logging;
using Monica.Core.Features.MoLogProvider;

namespace Monica.Core.Modularity.TypeFinder;

/// <summary>
/// Type finder that discovers and filters application types.
/// </summary>
/// <remarks>
/// Initializes a new domain type finder.
/// </remarks>
/// <param name="options">The type finder options.</param>
public class MoDomainTypeFinder(ModuleCoreOptionTypeFinder options) : IDomainTypeFinder
{
    public ILogger? Logger { get; set; } = LogProvider.For<MoDomainTypeFinder>(); 

    #region Fields

    private bool _assemblyListLoaded;
    private readonly List<Assembly> _assemblies = [];
    private readonly Dictionary<string, TypeFinderAssemblyLoadFailure> _assemblyLoadFailures =
        new(StringComparer.OrdinalIgnoreCase);

    #endregion
  
    #region Utilities

    /// <summary>
    /// Loads the assembly list once.
    /// </summary>
    protected virtual void LoadAssemblies()
    {
        if (_assemblyListLoaded)
            return;

        var scan = new TypeFinderAssemblyResolver(options, Logger).Resolve();
        MergeAssemblyLoadFailures(scan.FailedLoads.Values);
        _assemblies.AddRange(scan.Assemblies);

        Logger?.LogInformation(
            "Module system will scan the following assemblies:{Assemblies}",
            Environment.NewLine + string.Join(Environment.NewLine, _assemblies.Select(static assembly => assembly.GetName().Name).OrderBy(static name => name)));

        _assemblyListLoaded = true;
    }
    #endregion

    #region Methods

    /// <summary>
    /// Gets all related assemblies.
    /// </summary>
    /// <returns>The related assemblies.</returns>
    public virtual IEnumerable<Assembly> GetAssemblies()
    {
        LoadAssemblies();
        return _assemblies;
    }

    /// <inheritdoc />
    public TypeFinderAssemblyAnalysis GetAssemblyAnalysis()
    {
        LoadAssemblies();
        return new TypeFinderAssemblyAnalysisBuilder(options, _assemblies, _assemblyLoadFailures).Build();
    }

    public ModuleCoreOptionTypeFinder Options => options;

    /// <summary>
    /// Gets all types from the related assemblies.
    /// </summary>
    /// <returns>The discovered types.</returns>
    public virtual IEnumerable<Type> GetTypes()
    {
        LoadAssemblies();

        foreach (var assembly in _assemblies)
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                // Still return the types that were loaded successfully when referenced assemblies are missing.
                types = ex.Types.Where(t => t != null).ToArray()!;

                var loadFailure = TypeFinderAssemblyLoadFailure.CreateTypeScanFailure(assembly, ex);
                if (loadFailure != null)
                {
                    _assemblyLoadFailures[loadFailure.Name] = loadFailure;

                    Logger?.LogWarning(
                        "程序集 {AssemblyName} 部分类型加载失败: {Exceptions}",
                        loadFailure.Name,
                        loadFailure.ErrorMessage);
                }
            }

            foreach (var type in types)
            {
                yield return type;
            }
        }
    }

    #endregion

    private void MergeAssemblyLoadFailures(IEnumerable<TypeFinderAssemblyLoadFailure> failures)
    {
        foreach (var failure in failures)
        {
            _assemblyLoadFailures[failure.Name] = failure;
        }
    }
} 
