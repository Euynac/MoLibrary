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
        var input = reader.GetString();
        if (reader.TokenType == JsonTokenType.String)
        {
            foreach (var format in JsonSerializerOptionsProvider.DateTimeFormats)
            {
                if (DateTime.TryParseExact(input, format, null, DateTimeStyles.None, out var date))
                {
                    return JsonSerializerOptionsProvider.NormalizeInTime(date);
                }
            }

            if (DateTime.TryParse(input, out var defaultDate))
            {
                return JsonSerializerOptionsProvider.NormalizeInTime(defaultDate);
            }
        }

        return JsonSerializerOptionsProvider.NormalizeInTime(reader.GetDateTime());
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(JsonSerializerOptionsProvider.NormalizeOutTime(value).ToString(JsonSerializerOptionsProvider.OutputDateTimeFormat));
    }
}
