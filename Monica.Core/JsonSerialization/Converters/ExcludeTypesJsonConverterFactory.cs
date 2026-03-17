using System.Text.Json.Serialization;

namespace Monica.Core.JsonSerialization.Converters;

/// <summary>
/// Decorates another converter factory and skips conversion for explicitly ignored types.
/// This is mainly used when global enum-to-string conversion is enabled but certain types must keep their default behavior.
/// </summary>
/// <param name="innerFactory">The inner converter factory.</param>
/// <param name="ignoredTypes">The types that should bypass the inner converter.</param>
public class ExcludeTypesJsonConverterFactory(JsonConverterFactory innerFactory, params Type[] ignoredTypes) : JsonConverterFactoryDecorator(innerFactory)
{
    public HashSet<Type> IgnoredTypes { get; } = [.. ignoredTypes];
    public override bool CanConvert(Type typeToConvert)
    {
        return !IgnoredTypes.Contains(typeToConvert) && base.CanConvert(typeToConvert);
    }
}
