using System.Reflection;
using Microsoft.Extensions.Logging;
using MoLibrary.Core.Features.MoLogProvider;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.Core.Module.TypeFinder;

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

    #endregion
  
    #region Utilities

    /// <summary>
    /// 加载程序集列表
    /// </summary>
    protected virtual void LoadAssemblies()
    {
        if (_assemblyListLoaded)
            return;

        var entryAssembly = Assembly.GetEntryAssembly();
        if (entryAssembly == null)
            throw new InvalidOperationException("无法获取入口程序集");

        // 获取相关程序集
        var assemblies = entryAssembly.GetRelatedAssemblies(options.RelatedAssemblies);

        _assemblies.AddRange(assemblies.ToList());


        Logger?.LogInformation($"(入口程序集代码中未引用的程序集目前将被剪枝)模块系统遍历程序集：{_assemblies.Select(p => p.GetName().Name).OrderBy(p => p).StringJoin("\n")}");

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

                // 记录加载异常的详细信息
                if (Logger != null && ex.LoaderExceptions.Length > 0)
                {
                    var exceptionMessages = ex.LoaderExceptions
                        .Where(e => e != null)
                        .Select(e => e!.Message)
                        .Distinct();
                    Logger.LogWarning("程序集 {AssemblyName} 部分类型加载失败: {Exceptions}",
                        assembly.GetName().Name,
                        string.Join("; ", exceptionMessages));
                }
            }

            foreach (var type in types)
            {
                yield return type;
            }
        }
    }

    #endregion
} 