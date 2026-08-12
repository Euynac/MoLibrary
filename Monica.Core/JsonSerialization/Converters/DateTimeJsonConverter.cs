using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Monica.Core.JsonSerialization.Models;

namespace Monica.Core.JsonSerialization.Converters;

/// <summary>
/// JSON converter that serializes and deserializes <see cref="DateTime"/> values
/// with the date-time wire policy owned by the current Monica host.
/// </summary>
public class DateTimeJsonConverter(DateTimeWireFormat dateTimeFormat) : JsonConverter<DateTime>
{
    private readonly DateTimeWireFormat _dateTimeFormat = dateTimeFormat
        ?? throw new ArgumentNullException(nameof(dateTimeFormat));

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return ReadValue(ref reader);
    }

    internal DateTime ReadValue(ref Utf8JsonReader reader)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var input = reader.GetString();
            if (DateTimeWireFormat.TryParse(input, out var date))
            {
                return date;
            }

            if (DateTime.TryParse(
                    input,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var defaultDate))
            {
                return defaultDate;
            }
        }

        return reader.GetDateTime();
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        WriteValue(writer, value);
    }

    internal void WriteValue(Utf8JsonWriter writer, DateTime value)
    {
        writer.WriteStringValue(_dateTimeFormat.Format(value));
    }
}
