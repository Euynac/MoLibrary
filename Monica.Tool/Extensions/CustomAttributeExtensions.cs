using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Monica.Tool.Extensions;

/// <summary>
/// CustomAttribute extension method
/// </summary>
public static class CustomAttributeExtensions
{
    #region CachedCustomAttribute 缓存版的CustomAttribute

    /// <summary>
    /// Cache Data [Key composed of TypeName + AttributeName, Attribute object]
    /// </summary>
    private static readonly ConditionalWeakTable<Type, ConcurrentDictionary<(string? MemberName, Type AttributeType), object>> Cache = new();
    private static readonly object MissingAttribute = new();

    /// <summary>
    /// Get the CustomAttribute of the specified type
    /// </summary>
    /// <typeparam name="TAttribute">Attribute to obtain</typeparam>
    /// <returns>Returns the value of Attribute, if not, returns null</returns>
    public static TAttribute? GetCustomAttributeCached<TAttribute>(this Type classType)
        where TAttribute : Attribute
    {
        return GetCustomAttributeCached<TAttribute>(classType, null);
    }

    /// <summary>
    /// Get the CustomAttribute of the specified class or specified attribute or method
    /// </summary>
    /// <typeparam name="TAttribute">Attribute to obtain</typeparam>
    /// <typeparam name="TClass"></typeparam>
    /// <typeparam name="TProperty"></typeparam>
    /// <returns>Returns the value of Attribute, if not, returns null</returns>
    public static TAttribute? GetCustomAttributeCached<TAttribute, TClass, TProperty>(this Type classType,
        Expression<Func<TClass, TProperty>> property)
        where TAttribute : Attribute
    {
        var name = property.GetPropertyInfo().Name;
        return GetCustomAttributeCached<TAttribute>(classType, name);
    }

    /// <summary>
    /// Get the CustomAttribute of the specified class or specified attribute or method
    /// </summary>
    /// <typeparam name="TClass"></typeparam>
    /// <typeparam name="TProperty"></typeparam>
    /// <typeparam name="TAttribute"></typeparam>
    /// <returns>Returns the value of Attribute, if not, returns null</returns>
    public static TAttribute? GetCustomAttributeCached<TAttribute, TClass, TProperty>(this TClass classType,
        TAttribute attributeType, Expression<Func<TClass, TProperty>> property) where TAttribute : Attribute where TClass : class, new()
    {
        var name = property.GetPropertyInfo().Name;
        var type = typeof(TClass);
        return GetCustomAttributeCached<TAttribute>(type, name);
    }

    /// <summary>
    /// Get the CustomAttribute of the specified attribute or method of the specified class
    /// </summary>
    /// <typeparam name="TAttribute"></typeparam>
    /// <param name="sourceType">specified class</param>
    /// <param name="name">Specify attribute or method name</param>
    /// <returns>Returns the value of Attribute, if not, returns null</returns>
    public static TAttribute? GetCustomAttributeCached<TAttribute>(this Type sourceType, string? name)
        where TAttribute : Attribute
    {
        ArgumentNullException.ThrowIfNull(sourceType);

        var typeCache = Cache.GetValue(sourceType, static _ => new());
        var cacheKey = (name, typeof(TAttribute));
        var value = typeCache.GetOrAdd(cacheKey, _ => GetValue<TAttribute>(sourceType, name) ?? MissingAttribute);
        return ReferenceEquals(value, MissingAttribute) ? null : (TAttribute)value;
    }
    /// <summary>
    /// Gets the CustomAttribute of the specified class or its attributes or methods
    /// </summary>
    /// <typeparam name="TAttribute"></typeparam>
    /// <param name="type"></param>
    /// <param name="name">nameof</param>
    /// <returns>Returns the value of Attribute, if not, returns null</returns>
    private static TAttribute? GetValue<TAttribute>(Type type, string? name)
        where TAttribute : Attribute
    {
        if (string.IsNullOrEmpty(name))
        {
            return type.GetCustomAttribute<TAttribute>(false);
        }

        var methodInfo = type.GetMethod(name);
        if (methodInfo != null)
        {
            return methodInfo.GetCustomAttribute<TAttribute>(false);
        }
        var propertyInfo = type.GetProperty(name);
        if (propertyInfo != null)
        {
            return propertyInfo.GetCustomAttribute<TAttribute>(false);
        }

        var fieldInfo = type.GetField(name);
        if (fieldInfo != null)
        {
            return fieldInfo.GetCustomAttribute<TAttribute>(false);
        }

        return null;
    }

    #endregion

}
