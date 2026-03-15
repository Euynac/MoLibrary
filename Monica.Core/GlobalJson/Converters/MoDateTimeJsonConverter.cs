using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Monica.Core.GlobalJson.Converters;

/// <summary>
/// JSON converter that serializes and deserializes <see cref="DateTime"/> values
/// with the global Monica date-time formats.
/// </summary>
public class MoDateTimeJsonConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var input = reader.GetString();
        if (reader.TokenType == JsonTokenType.String)
        {
            foreach (var format in DefaultMoGlobalJsonOptions.DateTimeFormats)
            {
                if (DateTime.TryParseExact(input, format, null, DateTimeStyles.None, out var date))
                {
                    return DefaultMoGlobalJsonOptions.NormalizeInTime(date);
                }
            }

            if (DateTime.TryParse(input, out var defaultDate))
            {
                return DefaultMoGlobalJsonOptions.NormalizeInTime(defaultDate);
            }
        }

        return DefaultMoGlobalJsonOptions.NormalizeInTime(reader.GetDateTime());
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(DefaultMoGlobalJsonOptions.NormalizeOutTime(value).ToString(DefaultMoGlobalJsonOptions.OutputDateTimeFormat));
    }
}

/// <summary>
/// JSON converter that preserves the default <see cref="DateTime"/> serialization behavior for marked properties,
/// even when <see cref="MoDateTimeJsonConverter"/> is registered globally.
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
