using System.Text.Json.Serialization;

namespace MoLibrary.Core.GlobalJson.Converters;

/// <summary>
/// 可用于添加了全局Enum string转换但某些类型不需要Enum转换的的情况
/// </summary>
/// <param name="innerFactory"></param>
/// <param name="outTypes"></param>
public class OutJsonConverterFactory(JsonConverterFactory innerFactory, params Type[] outTypes) : JsonConverterFactoryDecorator(innerFactory)
{
    public HashSet<Type> OutTypes { get; } = [.. outTypes];
    public override bool CanConvert(Type typeToConvert)
    {
        return !OutTypes.Contains(typeToConvert) && base.CanConvert(typeToConvert);
    }
}