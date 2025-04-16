using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.GlobalJson.Converters;
using MoLibrary.Core.GlobalJson.Interfaces;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MoLibrary.Core.GlobalJson;

public static class MoGlobalJsonExtensions
{

    public static void AddMoGlobalJsonSerialization(this IServiceCollection services, Action<MoGlobalJsonOptions>? optionAction = null, Action<JsonSerializerOptions>? extendAction = null)
    {
        var extraOptions = new MoGlobalJsonOptions();
        if (optionAction is not null)
        {
            optionAction.Invoke(extraOptions);
            services.Configure(optionAction);
        }

        var options = new JsonSerializerOptions();
        extendAction?.Invoke(options);
        options.ConfigGlobalJsonSerializeOptions(extraOptions);
        DefaultMoGlobalJsonOptions.GlobalJsonSerializerOptions = options;

        //依赖于AsyncLocal技术，异步static单例，不同的请求线程会有不同的HttpContext
        services.AddHttpContextAccessor();

        //巨坑：minimal api 等全局注册
        services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(o =>
        {
            o.SerializerOptions.CloneFrom(DefaultMoGlobalJsonOptions.GlobalJsonSerializerOptions);
        });
        //MVC框架HTTP请求与响应的JsonConverter全局注册
        services.Configure<JsonOptions>(o =>
        {
            o.JsonSerializerOptions.CloneFrom(DefaultMoGlobalJsonOptions.GlobalJsonSerializerOptions);
        });

        services.AddSingleton<IGlobalJsonOption, DefaultMoGlobalJsonOptions>();

    }

    public static void ConfigGlobalJsonSerializeOptions(this JsonSerializerOptions options, MoGlobalJsonOptions extraOptions)
    {
        options.Converters.Add(new NullableDateTimeJsonConverter());
        options.Converters.Add(new DateTimeJsonConverter());
        //options.Converters.Add(new StringJsonConverter { HttpContextAccessor = httpContextAccessor });
        //options.Converters.Add(new JsonConverterFactoryForICollection { HttpContextAccessor = httpContextAccessor });
        options.Converters.Add(new NullableGuidJsonConverter());
        //options.Converters.Add(new JsonConverterFactoryForDtoObjectClass { HttpContextAccessor = httpContextAccessor });
        options.Converters.Add(new NullableLongToStringJsonConverter());

        // Add our EnumFormatValue converter if enabled
        if (extraOptions.EnableEnumFormatValue)
        {
            options.Converters.Add(new EnumFormatValueJsonConverterFactory());
        }

        if (extraOptions.EnableGlobalEnumToString)
        {
            options.Converters.Add(new ExcludeTypesJsonConverterFactory(new JsonStringEnumConverter(),
                [.. extraOptions.EnumTypeToIgnore ?? []])); //全局枚举对String、int转换支持
            //options.Converters.Add(new JsonStringEnumConverter()); //全局枚举对String、int转换支持
        }

        options.DefaultIgnoreCondition = extraOptions.DefaultIgnoreCondition;

        if (extraOptions.ReferenceHandlerPreserve)
        {
            options.ReferenceHandler = ReferenceHandler.Preserve;
        }

        //options.PropertyNameCaseInsensitive = true;

        //可以自动在string和long间转换？
        //options.NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString;
        options.NumberHandling = JsonNumberHandling.AllowReadingFromString;

        //options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
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