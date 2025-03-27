using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MoLibrary.Core.GlobalJson.Converters;

public class NullableDateTimeJsonConverter : JsonConverter<DateTime?>
{
    //巨坑：需要使用HandleNull才会进入
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
            if (DateTime.TryParseExact(input, DefaultMoGlobalJsonOptions.DateTimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                return DefaultMoGlobalJsonOptions.NormalizeInTime(date);
            }


            if (DateTime.TryParse(input, out var defaultDate))
            {
                return DefaultMoGlobalJsonOptions.NormalizeInTime(defaultDate);
            }
        }

        return DefaultMoGlobalJsonOptions.NormalizeInTime(reader.GetDateTime());
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }
        writer.WriteStringValue(DefaultMoGlobalJsonOptions.NormalizeOutTime(value.Value).ToString(DefaultMoGlobalJsonOptions.OutputDateTimeFormat));
    }
}