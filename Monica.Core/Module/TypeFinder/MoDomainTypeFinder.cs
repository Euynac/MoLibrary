using System.Reflection;
using Microsoft.Extensions.Logging;
using Monica.Core.Features.MoLogProvider;

namespace Monica.Core.Module.TypeFinder;

/// <summary>
/// 领域类型查找器，用于查找和筛选应用程序中的类型
/// </summary>
/// <remarks>
/// 初始化领域类型查找器的新实例
/// </remarks>
/// <param name="options">类型查找器配置选项</param>
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
    /// 加载程序集列表
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
    /// 获取所有相关程序集
    /// </summary>
    /// <returns>程序集集合</returns>
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
    /// 获取相关程序集所有类型
    /// </summary>
    /// <returns>所有类型的集合</returns>
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
                // 当程序集引用的其他程序集无法加载时，仍然返回能够加载的类型
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
