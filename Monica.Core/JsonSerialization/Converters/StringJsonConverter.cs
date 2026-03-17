using System.Text.Json;
using System.Text.Json.Serialization;

namespace Monica.Core.JsonSerialization.Converters;

public class StringJsonConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.GetString();
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }
        writer.WriteStringValue(value);
    }

    public override bool HandleNull => true;
}