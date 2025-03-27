using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MoLibrary.Core.GlobalJson.Converters;

public class DateTimeJsonConverter : JsonConverter<DateTime>
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