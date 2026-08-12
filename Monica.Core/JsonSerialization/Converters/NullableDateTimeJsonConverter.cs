using System.Text.Json;
using System.Text.Json.Serialization;
using Monica.Core.JsonSerialization.Models;

namespace Monica.Core.JsonSerialization.Converters;

/// <summary>
/// JSON converter that serializes and deserializes nullable <see cref="DateTime"/> values
/// with the date-time wire policy owned by the current Monica host.
/// </summary>
public class NullableDateTimeJsonConverter(DateTimeWireFormat dateTimeFormat) : JsonConverter<DateTime?>
{
    private readonly DateTimeJsonConverter _innerConverter = new(dateTimeFormat);

    // HandleNull must be enabled or null tokens never reach this converter.
    public override bool HandleNull => true;

    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null ||
            reader.TokenType == JsonTokenType.String && string.IsNullOrEmpty(reader.GetString()))
        {
            return null;
        }

        return _innerConverter.ReadValue(ref reader);
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        _innerConverter.WriteValue(writer, value.Value);
    }
}
