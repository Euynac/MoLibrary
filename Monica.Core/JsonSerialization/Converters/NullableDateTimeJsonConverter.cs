using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

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
            if (DateTime.TryParseExact(input, SharedJsonSerializerOptionsProvider.DateTimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                return SharedJsonSerializerOptionsProvider.NormalizeInTime(date);
            }


            if (DateTime.TryParse(input, out var defaultDate))
            {
                return SharedJsonSerializerOptionsProvider.NormalizeInTime(defaultDate);
            }
        }

        return SharedJsonSerializerOptionsProvider.NormalizeInTime(reader.GetDateTime());
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }
        writer.WriteStringValue(SharedJsonSerializerOptionsProvider.NormalizeOutTime(value.Value).ToString(SharedJsonSerializerOptionsProvider.OutputDateTimeFormat));
    }
}
