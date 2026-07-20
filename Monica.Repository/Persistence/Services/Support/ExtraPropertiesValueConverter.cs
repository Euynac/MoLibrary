using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Monica.Repository.Entity.Abstractions;
using Monica.Tool.Extensions;

namespace Monica.Repository.Persistence.Services.Support;

internal sealed class ExtraPropertiesValueConverter(Type entityType) : ValueConverter<ExtraPropertyDictionary, string>(
    d => SerializeObject(d, entityType),
    s => DeserializeObject(s, entityType))
{
    private static readonly JsonSerializerOptions SERIALIZE_OPTIONS = CreateReadOnlyOptions(new JsonSerializerOptions());

    private static string SerializeObject(ExtraPropertyDictionary extraProperties, Type? entityType)
    {
        var copyDictionary = new Dictionary<string, object?>(extraProperties);
        return JsonSerializer.Serialize(copyDictionary, SERIALIZE_OPTIONS);
    }

    private static readonly JsonSerializerOptions DESERIALIZE_OPTIONS = CreateReadOnlyOptions(
        new JsonSerializerOptions
        {
            Converters =
            {
                new ObjectToInferredTypesConverter()
            }
        });

    private static ExtraPropertyDictionary DeserializeObject(string extraPropertiesAsJson, Type? entityType)
    {
        if (extraPropertiesAsJson.IsNullOrEmpty() || extraPropertiesAsJson == "{}")
        {
            return new ExtraPropertyDictionary();
        }

        var dictionary = JsonSerializer.Deserialize<ExtraPropertyDictionary>(extraPropertiesAsJson, DESERIALIZE_OPTIONS) ??
                            new ExtraPropertyDictionary();

        return dictionary;
    }

    private static JsonSerializerOptions CreateReadOnlyOptions(JsonSerializerOptions options)
    {
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}



/// <summary>
/// https://docs.microsoft.com/en-us/dotnet/standard/serialization/system-text-json-converters-how-to#deserialize-inferred-types-to-object-properties
/// </summary>
internal sealed class ObjectToInferredTypesConverter : JsonConverter<object>
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
