using System.Text.Json;
using System.Text.Json.Serialization;

namespace Monica.Core.JsonSerialization.Converters;

/// <summary>
/// Preserves the original JSON shape while serializing enum values as strings.
/// </summary>
public class PreserveOriginalWithEnumStringConverter : JsonConverter<object>
{
    private static readonly JsonSerializerOptions OPTIONS = CreateOptions();

    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return JsonDocument.ParseValue(ref reader).RootElement.Clone();
    }

    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
    {
        if (value is JsonElement jsonElement)
        {
            jsonElement.WriteTo(writer);
        }
        else
        {
            JsonSerializer.Serialize(writer, value, value.GetType(), OPTIONS);
        }
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            Converters = { new JsonStringEnumConverter() }
        };
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
