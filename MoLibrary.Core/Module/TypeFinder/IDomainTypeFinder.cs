using System.Reflection;

namespace MoLibrary.Core.Module.TypeFinder;

/// <summary>
/// 定义类型查找器接口，用于在相关程序集中查找类型。一般用于业务程序集中类型的自动注册。
/// </summary>
public interface IDomainTypeFinder
{
    /// <summary>
    /// 查找所有类型
    /// </summary>
    /// <returns>所有找到的类型集合</returns>
    IEnumerable<Type> GetTypes();

    /// <summary>
    /// 获取所有相关程序集
    /// </summary>
    /// <returns>相关程序集集合</returns>
    IEnumerable<Assembly> GetAssemblies();
} 