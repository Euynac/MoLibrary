namespace Monica.Tool.Extensions;

/// <summary>
/// Extension methods of attribute related type.
/// </summary>
public static class AttributeExtensions
{
    #region Attribute类拓展
    /// <summary>
    /// Gets the CustomAttribute of the specified type on the type, including on its interface (only classes are supported, methods or attributes are not supported, etc.).
    /// <br/>English:  Get the CustomAttribute of the specified type on the type, including those on its interface (only class is supported, not method or property, etc.).
    /// </summary>
    /// <typeparam name="T">Specified attribute type</typeparam>
    /// <param name="type"></param>
    /// <returns></returns>
    public static IEnumerable<T> GetCustomAttributesIncludingBaseInterfaces<T>(this Type type)
    {
        var attributeType = typeof(T);
        return type.GetCustomAttributes(attributeType, true).
            Union(type.GetInterfaces().
                SelectMany(interfaceType => interfaceType.GetCustomAttributes(attributeType, true))).
            Distinct().Cast<T>();
    }
    #endregion
}