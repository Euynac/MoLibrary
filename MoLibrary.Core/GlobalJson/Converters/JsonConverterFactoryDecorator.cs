using System.Text.Json;
using System.Text.Json.Serialization;

namespace MoLibrary.Core.GlobalJson.Converters;

public class JsonConverterFactoryDecorator(JsonConverterFactory innerFactory) : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => innerFactory.CanConvert(typeToConvert);

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options) => innerFactory.CreateConverter(typeToConvert, options);
}