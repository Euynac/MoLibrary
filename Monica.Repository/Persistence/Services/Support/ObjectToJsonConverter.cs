using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Monica.Repository.Persistence.Services.Support;

public static class PropertyBuilderExtensions
{
    /// <summary>
    /// Stores an object as JSON and configures structural change tracking for mutable JSON-backed values.
    /// </summary>
    /// <typeparam name="TTargetObj">The JSON-backed property type.</typeparam>
    /// <param name="propertyBuilder">The EF Core property builder.</param>
    /// <param name="serializerOptions">
    /// Optional serializer policy. When omitted, default-valued members are not written.
    /// </param>
    /// <returns>The configured property builder.</returns>
    public static PropertyBuilder<TTargetObj> HasJsonConversion<TTargetObj>(
        this PropertyBuilder<TTargetObj> propertyBuilder,
        JsonSerializerOptions? serializerOptions = null)
    {
        var options = serializerOptions ?? ObjectToJsonConverter<TTargetObj>.DefaultSerializerOptions;
        return propertyBuilder.HasConversion(
            new ObjectToJsonConverter<TTargetObj>(options),
            new ObjectToJsonValueComparer<TTargetObj>(options));
    }
}

public class ObjectToJsonConverter<TTargetObj> : ValueConverter<TTargetObj, string>
{
    internal static JsonSerializerOptions DefaultSerializerOptions { get; } = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
    };

    public ObjectToJsonConverter(
        JsonSerializerOptions? serializerOptions = null,
        ConverterMappingHints? mappingHints = null)
        : base(
            ObjectToJson(serializerOptions ?? DefaultSerializerOptions),
            JsonToObject(serializerOptions ?? DefaultSerializerOptions),
            mappingHints)
    {
    }

    private static Expression<Func<TTargetObj, string>> ObjectToJson(JsonSerializerOptions serializerOptions)
    {
        return value => JsonSerializer.Serialize(value, serializerOptions);
    }

    private static Expression<Func<string, TTargetObj>> JsonToObject(JsonSerializerOptions serializerOptions)
    {
        return value => JsonSerializer.Deserialize<TTargetObj>(value, serializerOptions)!;
    }
}

/// <summary>
/// Compares and snapshots JSON-backed values through their serialized representation.
/// </summary>
/// <typeparam name="TTargetObj">The JSON-backed property type.</typeparam>
public sealed class ObjectToJsonValueComparer<TTargetObj> : ValueComparer<TTargetObj>
{
    public ObjectToJsonValueComparer(JsonSerializerOptions serializerOptions)
        : base(
            (left, right) => Serialize(left, serializerOptions) == Serialize(right, serializerOptions),
            value => StringComparer.Ordinal.GetHashCode(Serialize(value, serializerOptions)),
            value => Deserialize(Serialize(value, serializerOptions), serializerOptions))
    {
    }

    private static string Serialize(TTargetObj? value, JsonSerializerOptions serializerOptions)
    {
        return JsonSerializer.Serialize(value, serializerOptions);
    }

    private static TTargetObj Deserialize(string value, JsonSerializerOptions serializerOptions)
    {
        return JsonSerializer.Deserialize<TTargetObj>(value, serializerOptions)!;
    }
}
