using MoLibrary.Tool.Extensions;
using System.Reflection;

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

        foreach (var type in _assemblies.SelectMany(assembly => assembly.GetTypes()))
        {
            yield return type;
        }
    }

    #endregion
} 