using System.Text.Json.Serialization;

namespace MoLibrary.Core.GlobalJson.Converters;

/// <summary>
/// 可用于添加了全局Enum string转换但某些类型不需要Enum转换的的情况
/// </summary>
/// <param name="innerFactory"></param>
/// <param name="ignoredTypes"></param>
public class ExcludeTypesJsonConverterFactory(JsonConverterFactory innerFactory, params Type[] ignoredTypes) : JsonConverterFactoryDecorator(innerFactory)
{
    public HashSet<Type> IgnoredTypes { get; } = [.. ignoredTypes];
    public override bool CanConvert(Type typeToConvert)
    {
        return !IgnoredTypes.Contains(typeToConvert) && base.CanConvert(typeToConvert);
    }
}