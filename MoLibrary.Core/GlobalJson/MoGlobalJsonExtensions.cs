using MoLibrary.Core.GlobalJson.Converters;
using MoLibrary.Core.Modules;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MoLibrary.Core.GlobalJson;

public static class MoGlobalJsonExtensions
{
    public static void ConfigGlobalJsonSerializeOptions(this JsonSerializerOptions options, ModuleGlobalJsonOption extraOption)
    {
        options.Converters.Add(new NullableDateTimeJsonConverter());
        options.Converters.Add(new DateTimeJsonConverter());
        //options.Converters.Add(new StringJsonConverter { HttpContextAccessor = httpContextAccessor });
        //options.Converters.Add(new JsonConverterFactoryForICollection { HttpContextAccessor = httpContextAccessor });
        options.Converters.Add(new NullableGuidJsonConverter());
        //options.Converters.Add(new JsonConverterFactoryForDtoObjectClass { HttpContextAccessor = httpContextAccessor });
        options.Converters.Add(new NullableLongToStringJsonConverter());

        // Add our EnumFormatValue converter if enabled
        if (extraOption.EnableEnumFormatValue)
        {
            options.Converters.Add(new EnumFormatValueJsonConverterFactory());
        }

        if (extraOption.EnableGlobalEnumToString)
        {
            options.Converters.Add(new ExcludeTypesJsonConverterFactory(new JsonStringEnumConverter(),
                [.. extraOption.EnumTypeToIgnore ?? []])); //全局枚举对String、int转换支持
            //options.Converters.Add(new JsonStringEnumConverter()); //全局枚举对String、int转换支持
        }

        options.DefaultIgnoreCondition = extraOption.DefaultIgnoreCondition;

        if (extraOption.ReferenceHandlerPreserve)
        {
            options.ReferenceHandler = ReferenceHandler.Preserve;
        }

        options.PropertyNameCaseInsensitive = true;

        //可以自动在string和long间转换？
        //options.NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString;
        options.NumberHandling = JsonNumberHandling.AllowReadingFromString;

        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        //options.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
        options.DictionaryKeyPolicy = new JsonCamelCaseNamingPolicy();
        options.PropertyNamingPolicy = new JsonCamelCaseNamingPolicy();

        //允许注释
        //options.ReadCommentHandling = JsonCommentHandling.Skip;
    }
    public static JsonSerializerOptions Clone(this JsonSerializerOptions target)
    {
        var cloned = new JsonSerializerOptions();
        cloned.CloneFrom(target);
        return cloned;
    }
    public static void CloneFrom(this JsonSerializerOptions target, JsonSerializerOptions cloneFromOptions)
    {
        target.Converters.Clear();
        foreach (var converter in cloneFromOptions.Converters)
        {
            target.Converters.Add(converter);
        }


        target.PropertyNamingPolicy = cloneFromOptions.PropertyNamingPolicy;
        target.PropertyNameCaseInsensitive = cloneFromOptions.PropertyNameCaseInsensitive;
        target.DefaultIgnoreCondition = cloneFromOptions.DefaultIgnoreCondition;
        target.WriteIndented = cloneFromOptions.WriteIndented;
        target.Encoder = cloneFromOptions.Encoder;
        target.DefaultBufferSize = cloneFromOptions.DefaultBufferSize;
        target.DictionaryKeyPolicy = cloneFromOptions.DictionaryKeyPolicy;
        target.IgnoreReadOnlyProperties = cloneFromOptions.IgnoreReadOnlyProperties;
        target.IncludeFields = cloneFromOptions.IncludeFields;
        target.MaxDepth = cloneFromOptions.MaxDepth;
        target.NumberHandling = cloneFromOptions.NumberHandling;
        target.ReadCommentHandling = cloneFromOptions.ReadCommentHandling;
        target.AllowTrailingCommas = cloneFromOptions.AllowTrailingCommas;
    }

    internal static JsonSerializerOptions CloneButFilterConverter(this JsonSerializerOptions cloneFromOptions, Type filteredConverter)
    {
        var clonedOptions = new JsonSerializerOptions(cloneFromOptions);
        for (var index = 0; index < clonedOptions.Converters.Count; index++)
        {
            var converter = clonedOptions.Converters[index];
            if (converter.GetType() == filteredConverter)
            {
                clonedOptions.Converters.Remove(converter);
            }
        }

        return clonedOptions;
    }
}