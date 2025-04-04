using System.Text.Json;
using System.Text.Json.Serialization;

namespace MoLibrary.Core.GlobalJson.Converters;

/// <summary>
/// 保留原始Json格式但Enum转字符串输出
/// </summary>
public class PreserveOriginalWithEnumStringConverter : JsonConverter<object>
{
    internal static JsonSerializerOptions Options = new() { Converters = { new JsonStringEnumConverter() } };
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
            JsonSerializer.Serialize(writer, value, value.GetType(), Options);
        }
    }
}