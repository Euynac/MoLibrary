using System.Reflection;

namespace Monica.Tool.Reflection;

/// <summary>
/// Provides object-cloning and property-inspection helpers built on reflection.
/// </summary>
public static class ObjectReflection
{
    /// <summary>
    /// Extension of memberwise clone. Not recommend for production (Best practice is to each class needs clone define relative method). use for debug.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="obj"></param>
    /// <returns></returns>
    public static T ShallowCopy<T>(this T obj) where T : class
    {
        ArgumentNullException.ThrowIfNull(obj);

        var method = obj.GetType().GetMethod("MemberwiseClone", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (T) method.Invoke(obj, null)!;
    }

    /// <summary>
    /// Clone all writable attribute values in an object to the object (the reference type is still the same reference, and the value type is copied) (EFCore will track modifications because it is an Action operation)
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="obj"></param>
    /// <param name="copyFromObj">Objects whose values need to be copied are used for cloning</param>
    /// <param name="ignoreParameterNames">Set attribute names to ignore clones</param>
    /// <returns>Return given cloned object for convenient.</returns>
    public static T CloneParameters<T>(this T obj, T copyFromObj, params string[] ignoreParameterNames)
    {
        ArgumentNullException.ThrowIfNull(obj);
        ArgumentNullException.ThrowIfNull(copyFromObj);
        ArgumentNullException.ThrowIfNull(ignoreParameterNames);

        var ignoreList = ignoreParameterNames.ToHashSet(StringComparer.Ordinal);
        foreach (var propertyInfo in GetReadableProperties(typeof(T)).Where(p => p.CanWrite))
        {
            //Get attribute value:
            var propertyValue = propertyInfo.GetValue(copyFromObj);
            //Get attribute name:
            var propertyName = propertyInfo.Name;
            if (ignoreList.Contains(propertyName)) continue;
            propertyInfo.SetValue(obj, propertyValue);
        }
        return obj;
    }

    /// <summary>
    /// Use reflection to get all public property values.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="instance"></param>
    /// <returns></returns>
    public static IEnumerable<object?> GetPublicPropertyValues<T>(T instance) where T : class
    {
        ArgumentNullException.ThrowIfNull(instance);

        var type = typeof(T);
        foreach (var propertyInfo in GetReadableProperties(type))
        {
            //Get attribute value:
            yield return propertyInfo.GetValue(instance);
        }
    }


    /// <summary>
    /// Get all public attribute information in the specified class object and return Dict[key attribute name, Value[attribute type, attribute value]]
    /// </summary>
    /// <typeparam name="T">Specify type</typeparam>
    /// <param name="instance">class instance</param>
    /// <param name="toLowerCase">Whether the returned attribute name is converted to lowercase</param>
    /// <returns></returns>
    public static Dictionary<string, KeyValuePair<Type, object?>> GetAllPropertyInfo<T>(T instance, bool toLowerCase = false) where T : class
    {
        ArgumentNullException.ThrowIfNull(instance);

        var propertyInfoDict = new Dictionary<string, KeyValuePair<Type, object?>>(StringComparer.Ordinal);
        var type = typeof(T);
        foreach (var propertyInfo in GetReadableProperties(type))
        {
            //Get attribute type
            var propertyType = propertyInfo.PropertyType;
            //Get attribute name:
            var propertyName = toLowerCase ? propertyInfo.Name.ToLowerInvariant() : propertyInfo.Name;
            //Get attribute value:
            var propertyValue = propertyInfo.GetValue(instance);
            var propertyPair = new KeyValuePair<Type, object?>(propertyType, propertyValue);
            propertyInfoDict.Add(propertyName, propertyPair);
        }
        return propertyInfoDict;
    }

    private static IEnumerable<PropertyInfo> GetReadableProperties(Type type) =>
        type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.CanRead && property.GetIndexParameters().Length == 0);
}
