using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using System.Globalization;
using MoLibrary.Core.UtilsAbstract;

namespace BuildingBlocksPlatform.Converters;

public class MoGlobalJsonOptions
{
    /// <summary>Gets or sets a value that determines when properties with default values are ignored during serialization or deserialization.
    /// The default value is <see cref="F:System.Text.Json.Serialization.JsonIgnoreCondition.Never" />.</summary>
    /// <exception cref="T:System.ArgumentException">This property is set to <see cref="F:System.Text.Json.Serialization.JsonIgnoreCondition.Always" />.</exception>
    /// <exception cref="T:System.InvalidOperationException">This property is set after serialization or deserialization has occurred.
    /// 
    /// -or-
    /// 
    /// <see cref="P:System.Text.Json.JsonSerializerOptions.IgnoreNullValues" /> has been set to <see langword="true" />. These properties cannot be used together.</exception>
    public JsonIgnoreCondition DefaultIgnoreCondition { get; set; }
    /// <summary>
    /// Gets an object that indicates whether metadata properties are honored when JSON objects and arrays are deserialized into reference types, and written when reference types are serialized. This is necessary to create round-trippable JSON from objects that contain cycles or duplicate references.
    /// </summary>
    public bool ReferenceHandlerPreserve { get; set; }
    /// <summary>
    /// 当启用全局枚举转String输出时，忽略转换的枚举类型
    /// </summary>
    public List<Type>? EnumTypeToIgnore { get; set; }
    /// <summary>
    /// 启用全局枚举转String输出
    /// </summary>
    public bool EnableGlobalEnumToString { get; set; }
}

/// <summary>
/// 保留原始Json格式输出
/// </summary>
public class PreserveOriginalConverter : JsonConverter<object>
{
    //internal static JsonSerializerOptions Options = new() {Encoder = JavaScriptEncoder.Default};
    internal static JsonSerializerOptions Options = new();
    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return JsonDocument.ParseValue(ref reader).RootElement.Clone();
    }

    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
    {
        if (value is JsonElement jsonElement)
        {
            jsonElement.WriteTo(writer);
        }
        else
        {
            JsonSerializer.Serialize(writer, value, value.GetType(), Options);
        }
    }
}
/// <summary>
/// 保留原始Json格式但Enum转字符串输出
/// </summary>
public class PreserveOriginalWithEnumStringConverter : JsonConverter<object>
{
    internal static JsonSerializerOptions Options = new(){Converters = { new JsonStringEnumConverter()} };
    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return JsonDocument.ParseValue(ref reader).RootElement.Clone();
    }

    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
    {
        if (value is JsonElement jsonElement)
        {
            jsonElement.WriteTo(writer);
        }
        else
        {
            JsonSerializer.Serialize(writer, value, value.GetType(), Options);
        }
    }
}

///// <summary>
///// 自定义TimeSpan输出
///// </summary>
//public class TimeSpanCustomConverter : JsonConverter<TimeSpan>
//{
//    public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
//    {
//        var timeSpanString = reader.GetString();
//        if (TimeSpan.TryParse(timeSpanString, out TimeSpan value))
//        {
//            return value;
//        }

//        var parts = timeSpanString?.Split(":");
//        if (parts?.Length == 3 && int.TryParse(parts[0], out var hours) && int.TryParse(parts[1], out var min) &&
//            int.TryParse(parts[2], out var seconds))
//        {
//            return new TimeSpan(hours, min, seconds);
//        }

//        throw new FormatException($"Invalid TimeSpan format: {timeSpanString}");
//    }

//    public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
//    {
//        var formatted = $"{(int) value.TotalHours}:{value.Minutes:D2}:{value.Seconds:D2}";
//        writer.WriteStringValue(formatted);
//    }
//}



/// <summary>
/// long类型雪花ID精度损失问题，转string输出
/// </summary>
public class NullableLongToStringJsonConverter : JsonConverter<long>
{
    public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            if (long.TryParse(reader.GetString(), out var value)) return value;
        }

        return reader.GetInt64();
    }

    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}


//如果有多个相同类型的Converter，则只会使用前面注册的，ABP也有Guid?，所以需要替换掉。
public class NullableGuidJsonConverter : JsonConverter<Guid?>
{
    public override Guid? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;

        if (reader.TokenType == JsonTokenType.String)
        {
            var guidString = reader.GetString();
            string[] formats = ["N", "D", "B", "P", "X"];
            foreach (var format in formats)
            {
                if (Guid.TryParseExact(guidString, format, out var guid))
                {
                    return guid;
                }
            }
        }

        if (reader.TryGetGuid(out var guid2))
        {
            return guid2;
        }

        return null;
    }


    public override void Write(Utf8JsonWriter writer, Guid? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }
        writer.WriteStringValue(value.Value);
    }

    public override bool HandleNull => true;
}


//DateTime?只会处理DateTime?类型，DateTime类型不会处理。具体是因为CanConvert方法。
public class NullableDateTimeJsonConverter : JsonConverter<DateTime?>
{
    //巨坑：需要使用HandleNull才会进入
    public override bool HandleNull => true;

    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var input = reader.GetString();
        if (string.IsNullOrEmpty(input))
        {
            return null;
        }


        if (reader.TokenType == JsonTokenType.String)
        {
            if (DateTime.TryParseExact(input, JsonShared.DateTimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                return JsonShared.NormalizeInTime(date);
            }
         

            if (DateTime.TryParse(input, out var defaultDate))
            {
                return JsonShared.NormalizeInTime(defaultDate);
            }
        }

        return JsonShared.NormalizeInTime(reader.GetDateTime());
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }
        writer.WriteStringValue(JsonShared.NormalizeOutTime(value.Value).ToString(JsonShared.OutputDateTimeFormat));
    }
}

public class DateTimeJsonConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var input = reader.GetString();
        if (reader.TokenType == JsonTokenType.String)
        {
            foreach (var format in JsonShared.DateTimeFormats)
            {
                if (DateTime.TryParseExact(input, format, null, DateTimeStyles.None, out var date))
                {
                    return JsonShared.NormalizeInTime(date);
                }
            }

            if (DateTime.TryParse(input, out var defaultDate))
            {
                return JsonShared.NormalizeInTime(defaultDate);
            }
        }

        return JsonShared.NormalizeInTime(reader.GetDateTime());
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(JsonShared.NormalizeOutTime(value).ToString(JsonShared.OutputDateTimeFormat));
    }
}


public class StringJsonConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.GetString();
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }
        writer.WriteStringValue(value);
    }

    public override bool HandleNull => true;
}


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

public class JsonConverterFactoryDecorator(JsonConverterFactory innerFactory) : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => innerFactory.CanConvert(typeToConvert);

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options) => innerFactory.CreateConverter(typeToConvert, options);
}

public class AbpCamelCaseNamingPolicy : JsonNamingPolicy
{
    public override string ConvertName(string name) =>
        name.ToCamelCase(handleAbbreviations: true);
}




public static class JsonConverterExtensions
{
   
    public static void AddSharedJsonConverter(this IServiceCollection services, Action<MoGlobalJsonOptions>? optionAction = null)
    {
        var extraOptions = new MoGlobalJsonOptions();
        if (optionAction is not null)
        {
            optionAction.Invoke(extraOptions);
            services.Configure(optionAction);
        }

        var options = new JsonSerializerOptions();
        options.ConfigGlobalJsonSerializeOptions(extraOptions);
        JsonShared.GlobalJsonSerializerOptions = options;

        //依赖于AsyncLocal技术，异步static单例，不同的请求线程会有不同的HttpContext
        services.AddHttpContextAccessor();

        //巨坑：minimal api 等全局注册
        services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(o =>
        {
            o.SerializerOptions.CloneFrom(JsonShared.GlobalJsonSerializerOptions);
        });
        //MVC框架HTTP请求与响应的JsonConverter全局注册
        services.Configure<JsonOptions>(o =>
        {
            o.JsonSerializerOptions.CloneFrom(JsonShared.GlobalJsonSerializerOptions);
        });

        services.AddSingleton<IGlobalJsonOption, JsonShared>();

    }

    public static void ConfigGlobalJsonSerializeOptions(this JsonSerializerOptions options, MoGlobalJsonOptions extraOptions)
    {
        options.Converters.Add(new NullableDateTimeJsonConverter ());
        options.Converters.Add(new DateTimeJsonConverter ());
        //options.Converters.Add(new StringJsonConverter { HttpContextAccessor = httpContextAccessor });
        //options.Converters.Add(new JsonConverterFactoryForICollection { HttpContextAccessor = httpContextAccessor });
        options.Converters.Add(new NullableGuidJsonConverter());
        //options.Converters.Add(new JsonConverterFactoryForDtoObjectClass { HttpContextAccessor = httpContextAccessor });
        options.Converters.Add(new NullableLongToStringJsonConverter());

        if (extraOptions.EnableGlobalEnumToString)
        {
            options.Converters.Add(new OutJsonConverterFactory(new JsonStringEnumConverter(),
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
        options.DictionaryKeyPolicy = new AbpCamelCaseNamingPolicy();
        options.PropertyNamingPolicy = new AbpCamelCaseNamingPolicy();

        //允许注释
        //options.ReadCommentHandling = JsonCommentHandling.Skip;
    }
    public static JsonSerializerOptions Clone(this JsonSerializerOptions target)
    {
        var cloned = new JsonSerializerOptions();
        cloned.CloneFrom(target);
        return cloned;
    }
    internal static void CloneFrom(this JsonSerializerOptions target, JsonSerializerOptions cloneFromOptions)
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



public interface IHasHttpContextAccessor
{
    internal IHttpContextAccessor? HttpContextAccessor { get; set; }
}

public interface IJudgeBackendInvoke : IHasHttpContextAccessor
{
    public const string X_BACKEND_INVOKE = "X-Backend-Invoke";
    /// <summary>
    /// 用于区分前后端调用。
    /// </summary>
    /// <returns></returns>
    public bool IsBackendInvoke()
    {
        if (HttpContextAccessor?.HttpContext is {} context)
        {
            return context.Request.Headers.ContainsKey(X_BACKEND_INVOKE);
        }

        return true;
    }
}

#region JsonConverter null值转Json默认值
//public class JsonConverterFactoryForDtoObjectClass : JsonConverterFactory, IHasHttpContextAccessor
//{
//    public override bool CanConvert(Type typeToConvert)
//        => typeToConvert.IsClass
//           && typeToConvert.Name.StartsWith("Dto")
//           && !typeToConvert.IsAssignableTo<IDictionary>()
//           && typeToConvert is { IsGenericType: false, IsPublic: true };


//    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
//    {
//        var converterType = typeof(NullObjectToEmptyJsonConverter<>).MakeGenericType(typeToConvert);
//        //解决嵌套问题
//        var converter = Activator.CreateInstance(converterType,
//            options.CloneButFilterConverter(typeof(JsonConverterFactoryForDtoObjectClass)))!;
//        ((IHasHttpContextAccessor) converter).HttpContextAccessor = HttpContextAccessor;
//        return (JsonConverter) converter;
//    }

//    public IHttpContextAccessor? HttpContextAccessor { get; set; }
//}




////https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/converters-how-to
//public class JsonConverterFactoryForICollection : JsonConverterFactory, IHasHttpContextAccessor
//{
//    public override bool CanConvert(Type typeToConvert)
//        => typeToConvert.IsGenericType
//           && typeToConvert.IsAssignableTo<ICollection>()
//           && !typeToConvert.IsAssignableTo<IDictionary>();

//    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
//    {
//        var elementType = typeToConvert.GetGenericArguments()[0];
//        var converterType = typeof(NullCollectionToEmptyJsonConverter<>).MakeGenericType(elementType);
//        var converter = Activator.CreateInstance(converterType)!;
//        ((IHasHttpContextAccessor) converter).HttpContextAccessor = HttpContextAccessor;
//        return (JsonConverter) converter;
//    }

//    public IHttpContextAccessor? HttpContextAccessor { get; set; }
//}



//public class NullObjectToEmptyJsonConverter<T>(JsonSerializerOptions clonedOptions) : JsonConverterWithHttpContext<T?>
//{
//    public override bool HandleNull => true;

//    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
//    {
//        return JsonSerializer.Deserialize<T>(ref reader, clonedOptions);
//    }

//    public override void Write(Utf8JsonWriter writer, T? value, JsonSerializerOptions options)
//    {
//        if (value == null)
//        {
//            HandleNullValue(writer, EFrontendSupportType.Object, clonedOptions);
//            return;
//        }

//        JsonSerializer.Serialize(writer, value, clonedOptions);
//    }
//}
//public class NullCollectionToEmptyJsonConverter<T> : JsonConverterWithHttpContext<ICollection<T>?>
//{
//    public override bool HandleNull => true;

//    //还是必须要重写的
//    public override ICollection<T>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
//    {
//        return JsonSerializer.Deserialize<ICollection<T>>(ref reader, options);
//    }

//    public override void Write(Utf8JsonWriter writer, ICollection<T>? value, JsonSerializerOptions options)
//    {
//        if (value == null)
//        {
//            HandleNullValue(writer, EFrontendSupportType.ICollection, options);
//            return;
//        }
//        writer.WriteStartArray();
//        foreach (var item in value)
//        {
//            JsonSerializer.Serialize(writer, item, options);
//        }
//        writer.WriteEndArray();
//    }
//}


//public abstract class JsonConverterWithHttpContext<T> : JsonConverter<T>, IJudgeBackendInvoke
//{
//    public IHttpContextAccessor? HttpContextAccessor { get; set; }

//    protected void HandleNullValue(Utf8JsonWriter writer, EFrontendSupportType type, JsonSerializerOptions options)
//    {
//        writer.WriteNullValue();
//        //if (((IJudgeBackendInvoke)this).IsBackendInvoke() || options.DefaultIgnoreCondition == JsonIgnoreCondition.WhenWritingDefault || options.DefaultIgnoreCondition == JsonIgnoreCondition.WhenWritingNull)
//        //{
//        //    writer.WriteNullValue();
//        //}
//        //else
//        //{
//        //    switch (type)
//        //    {
//        //        case EFrontendSupportType.String:
//        //        case EFrontendSupportType.DateTime:
//        //            writer.WriteStringValue("");
//        //            break;
//        //        case EFrontendSupportType.ICollection:
//        //            writer.WriteStartArray();
//        //            writer.WriteEndArray();
//        //            break;
//        //        case EFrontendSupportType.Dictionary:
//        //        case EFrontendSupportType.Object:
//        //            writer.WriteStartObject();
//        //            writer.WriteEndObject();
//        //            break;
//        //        default:
//        //            throw new ArgumentOutOfRangeException(nameof(type), type, null);
//        //    }

//        //}
//    }

//    protected enum EFrontendSupportType
//    {
//        String,
//        DateTime,
//        ICollection,
//        Dictionary,
//        Object
//    }
//}


#endregion
