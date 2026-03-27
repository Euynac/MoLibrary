using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Monica.Core.JsonSerialization.Services;

namespace Monica.Core.JsonSerialization.Converters;

public class NullableDateTimeJsonConverter : JsonConverter<DateTime?>
{
    // HandleNull must be enabled or null tokens never reach this converter.
    public override bool HandleNull => true;

    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var input = reader.GetString();
        if (string.IsNullOrEmpty(input))
        {
            return null;
        }


        if (reader.TokenType == JsonTokenType.String)
        {
            if (DateTime.TryParseExact(input, JsonSerializerOptionsProvider.DateTimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                return JsonSerializerOptionsProvider.NormalizeInTime(date);
            }


            if (DateTime.TryParse(input, out var defaultDate))
            {
                return JsonSerializerOptionsProvider.NormalizeInTime(defaultDate);
            }
        }

        return JsonSerializerOptionsProvider.NormalizeInTime(reader.GetDateTime());
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }
        writer.WriteStringValue(JsonSerializerOptionsProvider.NormalizeOutTime(value.Value).ToString(JsonSerializerOptionsProvider.OutputDateTimeFormat));
    }
}
