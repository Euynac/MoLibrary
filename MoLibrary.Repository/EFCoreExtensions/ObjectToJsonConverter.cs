using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MoLibrary.Repository.EFCoreExtensions;

public static class PropertyBuilderExtensions
{
    /// <summary>
    /// EFCore扩展方法，使用JsonConverter将对象转换为Json字符串存储到数据库中
    /// </summary>
    /// <typeparam name="TTargetObj"></typeparam>
    /// <param name="propertyBuilder"></param>
    public static void HasJsonConversion<TTargetObj>(this PropertyBuilder<TTargetObj> propertyBuilder)
    {
        propertyBuilder.HasConversion(new ObjectToJsonConverter<TTargetObj>());
    }
}

public class ObjectToJsonConverter<TTargetObj>(ConverterMappingHints? mappingHints = null) : ValueConverter<TTargetObj, string>(ObjectToJson(), JsonToObject(), mappingHints)
{
    // ReSharper disable once StaticMemberInGenericType
    protected static JsonSerializerOptions SerializerOptions { get; set; } = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
    };
    public ObjectToJsonConverter() :this(null)
    {
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected static Expression<Func<TTargetObj, string>> ObjectToJson()
    {
        return v => JsonSerializer.Serialize(v, SerializerOptions);
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected static Expression<Func<string, TTargetObj>> JsonToObject()
    {
        return v => JsonSerializer.Deserialize<TTargetObj>(v, SerializerOptions)!;
    }
}