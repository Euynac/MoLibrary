using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using Monica.Tool.Extensions;

namespace Monica.Tool.General;

/// <summary>
/// Specify that an enumeration is the Name of KouEnum, and you can set the conversion name of the Enum.
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class KouEnumName : Attribute
{
    /// <summary>
    /// Use this type of string to convert to the Enum
    /// </summary>
    public string[] Names { get; }
    /// <summary>
    /// Indicates the name this enumeration has
    /// </summary>
    /// <param name="names"></param>
    public KouEnumName(params string[] names)
    {
        Names = names;
    }
}
/// <summary>
/// KouEnum tool can convert string to corresponding Enum
/// </summary>
public static class KouEnumTool
{
    private static readonly ConcurrentDictionary<Type, Dictionary<string, Enum>>
        _enumCache = new();
    /// <summary>
    /// Get the corresponding KouEnum tag enumeration through a string
    /// </summary>
    /// <param name="str"></param>
    /// <returns>Exception thrown on failure</returns>
    public static T ToKouEnum<T>(this string str) where T : struct, Enum
    {
        if (!TryToKouEnum(typeof(T), str, out var resultEnum))
        {
            throw new Exception($"{str} convert to {typeof(T).FullName} through KouEnumName failed");
        }
        return (T)resultEnum;
    }
    /// <summary>
    /// Try to get the corresponding KouEnum tag enumeration through a string
    /// </summary>
    /// <param name="str"></param>
    /// <param name="result"></param>
    /// <returns></returns>
    public static bool TryToKouEnum<T>(this string? str, out T result) where T : struct, Enum
    {
        result = default;
        if (!TryToKouEnum(typeof(T), str??"", out var resultEnum)) return false;
        result = (T)resultEnum;
        return true;
    }

    /// <summary>
    /// Try to obtain the corresponding KouEnum tag enumeration through string fuzzy
    /// </summary>
    /// <param name="str"></param>
    /// <param name="result"></param>
    /// <returns></returns>
    public static bool TryToKouEnumFuzzy<T>(this string? str, out List<T> result) where T : struct, Enum
    {
        result = new List<T>();
        if (!TryToKouEnum(typeof(T), str??"", out var resultEnum, true)) return false;
        result = ((IEnumerable)resultEnum).Cast<T>().ToList();
        return true;
    }
    /// <summary>
    /// Reads the <see cref="KouEnumName"/> attribute object from a marked <see cref="System.Enum"/> value.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static KouEnumName? GetKouEnumNameAttribute(this Enum? value) =>
        value?.GetType().GetCustomAttributeCached<KouEnumName>(value.ToString());

    /// <summary>
    /// Reads the No.<paramref name="valueAt"/> value from <see cref="KouEnumName"/> on a marked <see cref="System.Enum"/>.
    /// </summary>
    /// <param name="value">original <see cref="System.Enum"/> value</param>
    /// <param name="valueAt"> <see cref="KouEnumName"/> Which value in</param>
    /// <returns>Returns the value of the attribute tag if successfully retrieved, otherwise returns null</returns>
    public static string? GetKouEnumName(this Enum? value, int valueAt = 1)
        => value?.GetType().GetCustomAttributeCached<KouEnumName>(value.ToString())?.Names[valueAt - 1];
    /// <summary>
    /// Reads the <see cref="KouEnumName"/> value from a marked <see cref="System.Enum"/>, or falls back to ToString().
    /// </summary>
    /// <param name="value">original <see cref="System.Enum"/> value</param>
    /// <param name="valueAt"> <see cref="KouEnumName"/> Which value in</param>
    /// <returns>If successfully obtained, the value of the attribute tag is returned, otherwise it returns its own ToString.</returns>
    public static string? GetKouEnumNameOrString(this Enum? value, int valueAt = 1) => GetKouEnumName(value, valueAt) ?? value?.ToString();

    private static Dictionary<string, Enum> GetDict(Type type)
    {
        if (_enumCache.TryGetValue(type, out var enumDict))
        {
            return enumDict;
        }

        CreateCache(type); // Automatically create the enum cache when it does not exist.
        return _enumCache.TryGetValue(type, out enumDict)
            ? enumDict
            : throw new InvalidOperationException($"Failed to create enum cache for type {type.FullName}.");
    }

    /// <summary>
    /// Try to get the corresponding KouEnum tag enumeration through a string
    /// </summary>
    /// <param name="enumType">enum type</param>
    /// <param name="str"></param>
    /// <param name="result"></param>
    /// <param name="fuzzy">Fuzzy returns a List</param>
    /// <returns></returns>
    public static bool TryToKouEnum(Type enumType, string str, out object result, bool fuzzy = false)
    {
        result = default!;
        var enumDict = GetDict(enumType);
        switch (fuzzy)
        {
            case false when enumDict.TryGetValue(str, out var enumResult):
                result = enumResult;
                return true;
            case true:
            {
                var list = enumDict.Where(p => p.Key.Contains(str, StringComparison.OrdinalIgnoreCase)).Select(p => p.Value).ToList();
                result = list;
                return list.Count > 0;
            }
        }

        return false;
    }
    /// <summary>
    /// Create an enumeration name cache for this enum
    /// </summary>
    /// <param name="enumType"></param>
    private static void CreateCache(Type enumType)
    {
        var enumValues = Enum.GetValues(enumType);
        var enumNames = Enum.GetNames(enumType);
        Dictionary<string, Enum> enumsNameDict = new Dictionary<string, Enum>();
        var i = 0;
        foreach (var value in enumValues)
        {
            enumsNameDict.Add(enumNames[i], (Enum)value);
            var nameEnum = enumType.GetField(enumNames[i])?.GetCustomAttribute<KouEnumName>(false);
            i++;
            if (nameEnum == null) continue;
            foreach (var name in nameEnum.Names)
            {
                if (string.IsNullOrEmpty(name)) continue;
                enumsNameDict[name] = (Enum)value;
            }
                
        }
        _enumCache.TryAdd(enumType, enumsNameDict);
    }
}
