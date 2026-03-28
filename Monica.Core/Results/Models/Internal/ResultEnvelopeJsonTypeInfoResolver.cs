using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Monica.Core.Results.Internal;

internal sealed class ResultEnvelopeJsonTypeInfoResolver(
    ResultEnvelopeFieldNames fieldNames,
    IJsonTypeInfoResolver? innerResolver = null) : IJsonTypeInfoResolver
{
    private readonly ResultEnvelopeFieldNames _fieldNames = fieldNames ?? throw new ArgumentNullException(nameof(fieldNames));
    private readonly IJsonTypeInfoResolver _innerResolver = innerResolver ?? new DefaultJsonTypeInfoResolver();

    internal static IJsonTypeInfoResolver Create(
        ResultEnvelopeFieldNames fieldNames,
        IJsonTypeInfoResolver? innerResolver)
    {
        ArgumentNullException.ThrowIfNull(fieldNames);

        while (innerResolver is ResultEnvelopeJsonTypeInfoResolver resolver)
        {
            innerResolver = resolver._innerResolver;
        }

        return new ResultEnvelopeJsonTypeInfoResolver(fieldNames, innerResolver);
    }

    public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(options);

        var typeInfo = _innerResolver.GetTypeInfo(type, options);
        if (typeInfo is null || typeInfo.Kind != JsonTypeInfoKind.Object || !IsBuiltInResultEnvelopeType(type))
        {
            return typeInfo;
        }

        foreach (var property in typeInfo.Properties)
        {
            if (!_fieldNames.TryGetResolvedName(property.Name, options, out var resolvedName))
            {
                continue;
            }

            property.Name = resolvedName;
        }

        return typeInfo;
    }

    private static bool IsBuiltInResultEnvelopeType(Type type)
    {
        if (type == typeof(Res))
        {
            return true;
        }

        if (!type.IsGenericType)
        {
            return false;
        }

        var genericTypeDefinition = type.GetGenericTypeDefinition();
        return genericTypeDefinition == typeof(Res<>) || genericTypeDefinition == typeof(ResPaged<>);
    }
}
