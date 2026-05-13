using System.Text.Json;
using Monica.Configuration.Models;

namespace Monica.Configuration.Services.Support;

/// <summary>
/// Converts CLR values and stored payloads.
/// </summary>
internal sealed class ConfigurationStoredValueCodec
{
    /// <summary>
    /// Encodes a CLR value as a plain JSON stored value.
    /// </summary>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>The stored value.</returns>
    public ConfigurationStoredValue Encode<TValue>(TValue value)
    {
        return ConfigurationStoredValue.Plain(JsonSerializer.Serialize(value));
    }

    /// <summary>
    /// Converts a stored value into a Microsoft configuration scalar string.
    /// </summary>
    /// <param name="value">The stored value.</param>
    /// <returns>The scalar value.</returns>
    public string? ToConfigurationString(ConfigurationStoredValue value)
    {
        if (value.Kind != ConfigurationStoredValueKind.PlainJson)
        {
            return value.SecretReference is not null ? $"<secret:{value.SecretReference}>" : value.ProtectedPayload;
        }

        if (value.PlainJson is null)
        {
            return null;
        }

        using var document = JsonDocument.Parse(value.PlainJson);
        return document.RootElement.ValueKind switch
        {
            JsonValueKind.String => document.RootElement.GetString(),
            JsonValueKind.Number => document.RootElement.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => null,
            _ => document.RootElement.GetRawText()
        };
    }
}
