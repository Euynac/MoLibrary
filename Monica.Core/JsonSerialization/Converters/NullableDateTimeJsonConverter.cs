using System.Text.Json;
using System.Text.Json.Serialization;
using Monica.Core.JsonSerialization.Services;

namespace Monica.Core.JsonSerialization.Converters;

/// <summary>
/// JSON converter that serializes and deserializes nullable <see cref="DateTime"/> values
/// with the global Monica date-time formats.
/// </summary>
public class NullableDateTimeJsonConverter : JsonConverter<DateTime?>
{
    // HandleNull must be enabled or null tokens never reach this converter.
    public override bool HandleNull => true;

    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null ||
            reader.TokenType == JsonTokenType.String && string.IsNullOrEmpty(reader.GetString()))
        {
            return null;
        }

        return DateTimeJsonConverter.ReadValue(ref reader);
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        DateTimeJsonConverter.WriteValue(writer, value.Value);
    }
}
