using System.Globalization;
using System.Text.Json;

namespace Monica.SignalR.UISignalR.Support;

/// <summary>
/// Converts raw user input from the debug UI into typed SignalR invocation arguments.
/// </summary>
public sealed class SignalRInvocationArgumentParser
{
    /// <summary>
    /// Converts a raw string value into a CLR value suitable for JavaScript interop and SignalR invocation.
    /// </summary>
    /// <param name="value">The raw value entered by the user.</param>
    /// <param name="typeName">The target CLR type name.</param>
    public object? ConvertValue(string value, string typeName)
    {
        if (string.IsNullOrEmpty(value))
        {
            return GetDefaultValue(typeName);
        }

        var normalizedTypeName = NormalizeTypeName(typeName);

        return normalizedTypeName switch
        {
            "string" => value,
            "int" or "int32" => int.Parse(value.Trim(), CultureInfo.InvariantCulture),
            "long" or "int64" => long.Parse(value.Trim(), CultureInfo.InvariantCulture),
            "double" => double.Parse(value.Trim(), CultureInfo.InvariantCulture),
            "float" or "single" => float.Parse(value.Trim(), CultureInfo.InvariantCulture),
            "bool" or "boolean" => ParseBooleanValue(value),
            "datetime" => DateTime.Parse(value.Trim(), CultureInfo.InvariantCulture),
            "guid" => Guid.Parse(value.Trim()),
            _ when normalizedTypeName.Contains("[]", StringComparison.Ordinal)
                || normalizedTypeName.Contains("list", StringComparison.Ordinal)
                || normalizedTypeName.Contains("array", StringComparison.Ordinal) => TryParseAsJson(value),
            _ => TryParseComplexType(value)
        };
    }

    private static string NormalizeTypeName(string typeName)
    {
        return typeName.Trim().Replace("System.", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
    }

    private static bool ParseBooleanValue(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "true" or "1" or "yes" or "y" or "on" => true,
            "false" or "0" or "no" or "n" or "off" => false,
            _ => bool.Parse(value.Trim())
        };
    }

    private static object TryParseAsJson(string value)
    {
        var trimmedValue = value.Trim();

        if ((trimmedValue.StartsWith("[", StringComparison.Ordinal) && trimmedValue.EndsWith("]", StringComparison.Ordinal))
            || (trimmedValue.StartsWith("{", StringComparison.Ordinal) && trimmedValue.EndsWith("}", StringComparison.Ordinal)))
        {
            return JsonSerializer.Deserialize<object>(trimmedValue) ?? value;
        }

        return value;
    }

    private static object TryParseComplexType(string value)
    {
        return TryParseAsJson(value);
    }

    private static object GetDefaultValue(string typeName)
    {
        return NormalizeTypeName(typeName) switch
        {
            "string" => string.Empty,
            "int" or "int32" => 0,
            "long" or "int64" => 0L,
            "double" => 0d,
            "float" or "single" => 0f,
            "bool" or "boolean" => false,
            "datetime" => DateTime.UtcNow,
            "guid" => Guid.Empty,
            _ => string.Empty
        };
    }
}
