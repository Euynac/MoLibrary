using System.Text.Json;
using System.Text.Json.Serialization;

namespace Monica.Core.JsonSerialization.Converters;

/// <summary>
/// JSON converter that preserves the default <see cref="DateTime"/> serialization behavior for marked properties,
/// even when <see cref="DateTimeJsonConverter"/> is registered globally.
/// </summary>
public class PreserveOriginalDateTimeJsonConverter : JsonConverter<DateTime>
{
    /// <summary>
    /// Deserializes a <see cref="DateTime"/> by using the default JSON parsing behavior.
    /// </summary>
    /// <param name="reader">The JSON reader.</param>
    /// <param name="typeToConvert">The target type.</param>
    /// <param name="options">The serializer options.</param>
    /// <returns>The deserialized <see cref="DateTime"/>.</returns>
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var input = reader.GetString();
            if (DateTime.TryParse(input, out var result))
            {
                return result;
            }
        }

        return reader.GetDateTime();
    }

    /// <summary>
    /// Serializes a <see cref="DateTime"/> by using the default round-trip JSON format.
    /// </summary>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="value">The date-time value to serialize.</param>
    /// <param name="options">The serializer options.</param>
    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString("O")); // Uses the ISO 8601 round-trip format.
    }
}
