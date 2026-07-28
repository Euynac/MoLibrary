using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Monica.Core.JsonSerialization.Services;

namespace Monica.Core.JsonSerialization.Converters;

/// <summary>
/// JSON converter that serializes and deserializes <see cref="DateTime"/> values
/// with the global Monica date-time formats.
/// </summary>
public class DateTimeJsonConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return ReadValue(ref reader);
    }

    internal static DateTime ReadValue(ref Utf8JsonReader reader)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var input = reader.GetString();
            if (DateTime.TryParseExact(
                    input,
                    JsonSerializerOptionsProvider.DateTimeFormats,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var date))
            {
                return JsonSerializerOptionsProvider.NormalizeInTime(date);
            }

            if (DateTime.TryParse(
                    input,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var defaultDate))
            {
                return JsonSerializerOptionsProvider.NormalizeInTime(defaultDate);
            }
        }

        return JsonSerializerOptionsProvider.NormalizeInTime(reader.GetDateTime());
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        WriteValue(writer, value);
    }

    internal static void WriteValue(Utf8JsonWriter writer, DateTime value)
    {
        writer.WriteStringValue(JsonSerializerOptionsProvider.NormalizeOutTime(value).ToString(
            JsonSerializerOptionsProvider.OutputDateTimeFormat,
            CultureInfo.InvariantCulture));
    }
}
