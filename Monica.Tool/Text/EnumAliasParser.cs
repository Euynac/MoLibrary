using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using Monica.Tool.Annotations;
using Monica.Tool.Extensions;

namespace Monica.Tool.Text;

/// <summary>
/// Parses enum values from their declared aliases.
/// </summary>
public static class EnumAliasParser
{
    private static readonly ConcurrentDictionary<Type, Dictionary<string, Enum>> EnumCache = new();

    /// <summary>
    /// Parses the supplied alias into an enum value.
    /// </summary>
    public static T ParseAlias<T>(this string str) where T : struct, Enum
    {
        if (!TryParseAlias(typeof(T), str, out var resultEnum))
        {
            throw new InvalidOperationException(
                $"'{str}' could not be converted to {typeof(T).FullName} using {nameof(EnumAliasAttribute)}.");
        }

        return (T)resultEnum;
    }

    /// <summary>
    /// Tries to parse the supplied alias into an enum value.
    /// </summary>
    public static bool TryParseAlias<T>(this string? str, out T result) where T : struct, Enum
    {
        result = default;
        if (!TryParseAlias(typeof(T), str ?? string.Empty, out var resultEnum))
        {
            return false;
        }

        result = (T)resultEnum;
        return true;
    }

    /// <summary>
    /// Tries to resolve aliases using case-insensitive substring matching.
    /// </summary>
    public static bool TryParseAliasFuzzy<T>(this string? str, out List<T> result) where T : struct, Enum
    {
        result = [];
        if (string.IsNullOrWhiteSpace(str))
        {
            return false;
        }

        if (!TryParseAlias(typeof(T), str ?? string.Empty, out var resultEnum, true))
        {
            return false;
        }

        result = ((IEnumerable)resultEnum).Cast<T>().Distinct().ToList();
        return true;
    }

    /// <summary>
    /// Gets the alias attribute applied to the enum member, if present.
    /// </summary>
    public static EnumAliasAttribute? GetEnumAliasAttribute(this Enum? value) =>
        value?.GetType().GetCustomAttributeCached<EnumAliasAttribute>(value.ToString());

    /// <summary>
    /// Gets a specific alias from the enum member.
    /// </summary>
    public static string? GetEnumAlias(this Enum? value, int valueAt = 1)
    {
        if (value is null)
        {
            return null;
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(valueAt, 1);
        var names = value.GetType().GetCustomAttributeCached<EnumAliasAttribute>(value.ToString())?.Names;
        return names is not null && valueAt <= names.Length ? names[valueAt - 1] : null;
    }

    /// <summary>
    /// Gets an alias when available, otherwise falls back to the enum member name.
    /// </summary>
    public static string? GetEnumAliasOrString(this Enum? value, int valueAt = 1) => GetEnumAlias(value, valueAt) ?? value?.ToString();

    private static Dictionary<string, Enum> GetDictionary(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (!type.IsEnum)
        {
            throw new ArgumentException($"Type {type.FullName} is not an enum.", nameof(type));
        }

        return EnumCache.GetOrAdd(type, CreateCache);
    }

    /// <summary>
    /// Tries to parse an alias against a runtime enum type.
    /// </summary>
    public static bool TryParseAlias(Type enumType, string str, out object result, bool fuzzy = false)
    {
        result = default!;
        ArgumentNullException.ThrowIfNull(enumType);
        ArgumentNullException.ThrowIfNull(str);
        if (!enumType.IsEnum || fuzzy && string.IsNullOrWhiteSpace(str))
        {
            return false;
        }

        var enumDict = GetDictionary(enumType);
        switch (fuzzy)
        {
            case false when enumDict.TryGetValue(str, out var enumResult):
                result = enumResult;
                return true;
            case true:
                var list = enumDict
                    .Where(p => p.Key.Contains(str, StringComparison.OrdinalIgnoreCase))
                    .Select(p => p.Value)
                    .ToList();
                result = list;
                return list.Count > 0;
        }

        return false;
    }

    /// <summary>
    /// Builds the alias cache for the supplied enum type.
    /// </summary>
    private static Dictionary<string, Enum> CreateCache(Type enumType)
    {
        var enumValues = Enum.GetValues(enumType);
        var enumNames = Enum.GetNames(enumType);

        Dictionary<string, Enum> enumNameDictionary = new(StringComparer.Ordinal);
        var i = 0;

        foreach (var value in enumValues)
        {
            enumNameDictionary.Add(enumNames[i], (Enum)value);
            var aliasAttribute = enumType.GetField(enumNames[i])?.GetCustomAttribute<EnumAliasAttribute>(false);
            i++;

            if (aliasAttribute == null)
            {
                continue;
            }

            foreach (var name in aliasAttribute.Names)
            {
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                enumNameDictionary[name] = (Enum)value;
            }
        }

        return enumNameDictionary;
    }
}
