using System.Text.Json;
using System.Text.Json.Serialization;

namespace Monica.Core.JsonSerialization.Converters;

/// <summary>
/// Writes object values by preserving their original JSON shape whenever possible.
/// </summary>
public class PreserveOriginalConverter : JsonConverter<object>
{
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
            JsonSerializer.Serialize(writer, value, value.GetType(), JsonSerializerOptions.Default);
        }
    }
}

/// <summary>
/// Writes typed values by preserving their original JSON shape whenever possible.
/// </summary>
public class PreserveOriginalConverter<T> : JsonConverter<T>
{
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
        JsonSerializer.Serialize(writer, value, value.GetType(), JsonSerializerOptions.Default);
    }
}
