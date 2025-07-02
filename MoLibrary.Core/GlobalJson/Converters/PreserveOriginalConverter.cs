using System.Text.Json;
using System.Text.Json.Serialization;

namespace MoLibrary.Core.GlobalJson.Converters;

/// <summary>
/// 保留原始Json格式输出
/// </summary>
public class PreserveOriginalConverter : JsonConverter<object>
{
    //internal static JsonSerializerOptions Options = new() {Encoder = JavaScriptEncoder.Default};
    internal static JsonSerializerOptions Options = new();
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

/// <summary>
/// 保留原始Json格式输出
/// </summary>
public class PreserveOriginalConverter<T> : JsonConverter<T>
{
    //internal static JsonSerializerOptions Options = new() {Encoder = JavaScriptEncoder.Default};
    internal static JsonSerializerOptions Options = new();
  
    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return JsonSerializer.Deserialize<T?>(ref reader, options);
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        if(value == null)
        {
            writer.WriteNullValue();
            return;
        }
        JsonSerializer.Serialize(writer, value, value.GetType(), Options);
    }
}