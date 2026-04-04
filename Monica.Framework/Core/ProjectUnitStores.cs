using Monica.Framework.Core.Interfaces;
using Monica.Framework.Core.Model;

namespace Monica.Framework.Core;

/// <summary>
/// Dictionaries all use FullName for matching
/// </summary>
public static class ProjectUnitStores
{
    /// <summary>
    /// Project Unit List Project Unit TypeFullName
    /// </summary>
    internal static Dictionary<string, ProjectUnit> ProjectUnitsByFullName { get; } = [];

    /// <summary>
    /// Project unit list Project unit type Name
    /// </summary>
    internal static Dictionary<string, ProjectUnit> ProjectUnitsByName { get; } = [];

    /// <summary>
    /// All enumeration information of the project Enumeration type Name, not FullName
    /// </summary>
    internal static Dictionary<string, Type> EnumTypes { get; }= [];

    /// <summary>
    /// Get the specified project unit properties
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
    /// Get the specified project unit properties
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
    /// Get all project units
    /// </summary>
    /// <returns></returns>
    public static List<ProjectUnit> GetAllUnits()
    {
        return [.. ProjectUnitsByFullName.Values];
    }
    /// <summary>
    /// Get the project unit of the specified type (the entity needs to inherit IEntity)
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
    /// Get the project unit of the specified type (the entity needs to inherit IEntity)
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static T? GetUnitByName<T>(string key) where T : ProjectUnit
    {
        if (ProjectUnitsByName.TryGetValue(key, out var unit) && unit is T u)
        {
            return u;
        }

        return null;
    }
    /// <summary>
    /// Get the project unit of the specified type
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static IEnumerable<T> GetUnits<T>() where T :ProjectUnit
    {
        return ProjectUnitsByFullName.Values.OfType<T>();
    }
}