using System.Text.Json;
using Monica.Configuration.Models;
using Monica.Configuration.Providers.JsonFile;
using Monica.Configuration.Services.Support;

namespace Monica.Configuration.UI.Support;

/// <summary>
/// Provides consistent display formatting for configuration values in the UI.
/// </summary>
internal static class ConfigurationDisplayHelper
{
    /// <summary>
    /// User-facing text shown when a sensitive option has no configured value.
    /// </summary>
    public const string NotSetText = "未设置";

    /// <summary>
    /// Formats a configuration value for inline display.
    /// </summary>
    /// <param name="option">Option metadata.</param>
    /// <param name="value">Value to display.</param>
    /// <param name="maxLength">Optional maximum text length.</param>
    /// <returns>Formatted display text.</returns>
    public static string GetDisplayText(ConfigurationOptionSnapshot option, object? value, int? maxLength = null)
    {
        var text = option.IsSensitive
            ? GetSensitiveDisplayText(option, value)
            : SerializeForDisplay(value);

        if (!maxLength.HasValue || text.Length <= maxLength.Value)
        {
            return text;
        }

        var visibleLength = Math.Max(0, maxLength.Value - 3);
        return $"{text[..visibleLength]}...";
    }

    /// <summary>
    /// Serializes a configuration value for JSON diff and preview surfaces.
    /// </summary>
    /// <param name="option">Option metadata.</param>
    /// <param name="value">Value to serialize.</param>
    /// <returns>JSON text safe for UI display.</returns>
    public static string GetJsonText(ConfigurationOptionSnapshot option, object? value)
    {
        if (option.IsSensitive)
        {
            var maskedValue = HasSensitiveValue(option, value)
                ? ConfigurationSensitiveDataRedactor.MaskedValue
                : null;
            return JsonSerializer.Serialize(maskedValue, JsonFileConventions.JsonSerializerOptions);
        }

        return SerializeAsJson(value);
    }

    private static string GetSensitiveDisplayText(ConfigurationOptionSnapshot option, object? value)
    {
        return HasSensitiveValue(option, value)
            ? ConfigurationSensitiveDataRedactor.MaskedValue
            : NotSetText;
    }

    private static bool HasSensitiveValue(ConfigurationOptionSnapshot option, object? value)
    {
        return ConfigurationSensitiveDataRedactor.HasStoredValue(value) || option.HasStoredValue;
    }

    private static string SerializeForDisplay(object? value)
    {
        if (value == null)
        {
            return "null";
        }

        if (value is string stringValue)
        {
            return stringValue;
        }

        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Null or JsonValueKind.Undefined => "null",
                JsonValueKind.String => element.GetString() ?? string.Empty,
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Number => element.ToString(),
                _ => SerializeAsJson(value)
            };
        }

        return SerializeAsJson(value);
    }

    private static string SerializeAsJson(object? value)
    {
        try
        {
            return JsonSerializer.Serialize(value, JsonFileConventions.JsonSerializerOptions);
        }
        catch
        {
            return value?.ToString() ?? "null";
        }
    }
}
