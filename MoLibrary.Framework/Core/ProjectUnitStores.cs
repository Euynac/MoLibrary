using MoLibrary.Framework.Core.Interfaces;
using MoLibrary.Framework.Core.Model;

namespace MoLibrary.Framework.Core;

/// <summary>
/// 字典均使用FullName进行匹配
/// </summary>
public static class ProjectUnitStores
{
    /// <summary>
    /// 项目单元列表 项目单元类型FullName
    /// </summary>
    internal static Dictionary<string, ProjectUnit> ProjectUnitsByFullName { get; } = [];

    /// <summary>
    /// 项目单元列表 项目单元类型Name
    /// </summary>
    internal static Dictionary<string, ProjectUnit> ProjectUnitsByName { get; } = [];

    /// <summary>
    /// 项目所有枚举信息 枚举类型Name，非FullName
    /// </summary>
    internal static Dictionary<string, Type> EnumTypes { get; }= [];

    /// <summary>
    /// 获取指定项目单元特性
    /// </summary>
    /// <typeparam name="TAttribute"></typeparam>
    /// <param name="typeFullName"></param>
    /// <returns></returns>
    public static TAttribute? GetUnitAttributeByFullName<TAttribute>(string? typeFullName) where TAttribute : Attribute, IUnitCachedAttribute
    {
        if (string.IsNullOrWhiteSpace(typeFullName)) return null;

        return !ProjectUnitsByFullName.TryGetValue(typeFullName, out var unit) ? null : unit.Attributes.OfType<TAttribute>().FirstOrDefault();
    }
    /// <summary>
    /// 获取指定项目单元特性
    /// </summary>
    /// <typeparam name="TAttribute"></typeparam>
    /// <param name="typeName"></param>
    /// <returns></returns>
    public static TAttribute? GetUnitAttributeByName<TAttribute>(string? typeName) where TAttribute : Attribute, IUnitCachedAttribute
    {
        if (string.IsNullOrWhiteSpace(typeName)) return null;

        return !ProjectUnitsByName.TryGetValue(typeName, out var unit) ? null : unit.Attributes.OfType<TAttribute>().FirstOrDefault();
    }
    /// <summary>
    /// 获取所有项目单元
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static List<ProjectUnit> GetAllUnits()
    {
        return [.. ProjectUnitsByFullName.Values];
    }
    /// <summary>
    /// 获取指定类型的项目单元（实体需继承IMoEntity）
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static T? GetUnit<T>(string key) where T : ProjectUnit
    {
        if (ProjectUnitsByFullName.TryGetValue(key, out var unit) && unit is T u)
        {
            return u;
        }

        return null;
    }

    /// <summary>
    /// 获取指定类型的项目单元
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static IEnumerable<T> GetUnits<T>() where T :ProjectUnit
    {
        return ProjectUnitsByFullName.Values.OfType<T>();
    }
}