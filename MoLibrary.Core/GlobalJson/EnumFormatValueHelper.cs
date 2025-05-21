using System.Collections.Concurrent;
using System.Reflection;
using MoLibrary.Core.GlobalJson.Attributes;

namespace MoLibrary.Core.GlobalJson;

/// <summary>
/// Provides helper methods for working with enum format values.
/// </summary>
public static class EnumFormatValueHelper
{
    // Caches for better performance
    private static readonly ConcurrentDictionary<Type, ConcurrentDictionary<string, object>> FormattedStringToEnumCache = new();
    private static readonly ConcurrentDictionary<Type, ConcurrentDictionary<object, string>> EnumToFormattedStringCache = new();

    /// <summary>
    /// Gets the formatted value for an enum value.
    /// </summary>
    /// <param name="enumValue">The enum value.</param>
    /// <returns>The formatted value if specified with <see cref="EnumFormatValueAttribute"/>, otherwise the enum value's string representation.</returns>
    public static string GetFormattedValue(Enum enumValue)
    {
        if (enumValue == null)
            throw new ArgumentNullException(nameof(enumValue));

        var enumType = enumValue.GetType();
        var enumValueObj = (object)enumValue;

        // Check cache first
        if (EnumToFormattedStringCache.TryGetValue(enumType, out var valueCache) &&
            valueCache.TryGetValue(enumValueObj, out var cachedFormattedValue))
        {
            return cachedFormattedValue;
        }

        // Not in cache, look up the attribute
        var fieldInfo = enumType.GetField(enumValue.ToString());
        if (fieldInfo == null)
            return enumValue.ToString();

        var attribute = fieldInfo.GetCustomAttribute<EnumFormatValueAttribute>();
        var formattedValue = attribute?.FormattedValue ?? enumValue.ToString();

        // Add to cache
        valueCache = EnumToFormattedStringCache.GetOrAdd(enumType, _ => new ConcurrentDictionary<object, string>());
        valueCache.AddOrUpdate(enumValueObj, formattedValue, (_, __) => formattedValue);

        return formattedValue;
    }

    /// <summary>
    /// Parses a formatted string value to an enum value.
    /// </summary>
    /// <typeparam name="TEnum">The enum type.</typeparam>
    /// <param name="formattedValue">The formatted value to parse.</param>
    /// <returns>The corresponding enum value.</returns>
    /// <exception cref="ArgumentException">Thrown when the formatted value cannot be parsed to the enum type.</exception>
    public static TEnum ParseFormattedValue<TEnum>(string formattedValue) where TEnum : struct, Enum
    {
        if (TryParseFormattedValue<TEnum>(formattedValue, out var result))
            return result;

        throw new ArgumentException($"The formatted value '{formattedValue}' could not be parsed to enum type {typeof(TEnum).Name}");
    }

    /// <summary>
    /// Tries to parse a formatted string value to an enum value.
    /// </summary>
    /// <typeparam name="TEnum">The enum type.</typeparam>
    /// <param name="formattedValue">The formatted value to parse.</param>
    /// <param name="result">When this method returns, contains the parsed enum value if successful; otherwise, the default value.</param>
    /// <returns>true if the conversion succeeded; otherwise, false.</returns>
    public static bool TryParseFormattedValue<TEnum>(string formattedValue, out TEnum result) where TEnum : struct, Enum
    {
        if (string.IsNullOrEmpty(formattedValue))
        {
            result = default;
            return false;
        }

        var enumType = typeof(TEnum);

        // Try standard enum parsing first
        if (Enum.TryParse(formattedValue, true, out result))
            return true;

        // Check cache
        if (FormattedStringToEnumCache.TryGetValue(enumType, out var valueCache) &&
            valueCache.TryGetValue(formattedValue, out var cachedEnum))
        {
            result = (TEnum)cachedEnum;
            return true;
        }

        // Not in cache, look up by attributes
        foreach (var field in enumType.GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var attribute = field.GetCustomAttribute<EnumFormatValueAttribute>();
            if (attribute == null ||
                !string.Equals(attribute.FormattedValue, formattedValue, StringComparison.Ordinal)) continue;
            var enumValue = (TEnum)field.GetValue(null)!;
                
            // Add to cache
            valueCache = FormattedStringToEnumCache.GetOrAdd(enumType, _ => new ConcurrentDictionary<string, object>());
            valueCache.AddOrUpdate(formattedValue, enumValue, (_, __) => enumValue);
                
            result = enumValue;
            return true;
        }

        result = default;
        return false;
    }
} 