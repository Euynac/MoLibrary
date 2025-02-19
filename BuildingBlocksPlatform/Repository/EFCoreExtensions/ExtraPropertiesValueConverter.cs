using System.Text.Json.Serialization;
using BuildingBlocksPlatform.Repository.EntityInterfaces;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BuildingBlocksPlatform.Repository.EFCoreExtensions;

public class ExtraPropertiesValueConverter(Type entityType) : ValueConverter<ExtraPropertyDictionary, string>(
    d => SerializeObject(d, entityType),
    s => DeserializeObject(s, entityType))
{
    public static readonly JsonSerializerOptions SerializeOptions = new();

    private static string SerializeObject(ExtraPropertyDictionary extraProperties, Type? entityType)
    {
        var copyDictionary = new Dictionary<string, object?>(extraProperties);
        return JsonSerializer.Serialize(copyDictionary, SerializeOptions);
    }

    public static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        Converters =
        {
            new ObjectToInferredTypesConverter()
        }
    };

    private static ExtraPropertyDictionary DeserializeObject(string extraPropertiesAsJson, Type? entityType)
    {
        if (extraPropertiesAsJson.IsNullOrEmpty() || extraPropertiesAsJson == "{}")
        {
            return new ExtraPropertyDictionary();
        }

        var dictionary = JsonSerializer.Deserialize<ExtraPropertyDictionary>(extraPropertiesAsJson, DeserializeOptions) ??
                            new ExtraPropertyDictionary();

        return dictionary;
    }
}



/// <summary>
/// https://docs.microsoft.com/en-us/dotnet/standard/serialization/system-text-json-converters-how-to#deserialize-inferred-types-to-object-properties
/// </summary>
public class ObjectToInferredTypesConverter : JsonConverter<object>
{
    public override object Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) => (reader.TokenType switch
    {
        JsonTokenType.True => true,
        JsonTokenType.False => false,
        JsonTokenType.Number when reader.TryGetInt64(out long l) => l,
        JsonTokenType.Number => reader.GetDouble(),
        JsonTokenType.String when reader.TryGetDateTime(out DateTime datetime) => datetime,
        JsonTokenType.String => reader.GetString(),
        _ => JsonDocument.ParseValue(ref reader).RootElement.Clone()
    })!;

    public override void Write(
        Utf8JsonWriter writer,
        object objectToWrite,
        JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, objectToWrite, objectToWrite.GetType(), options);
}
