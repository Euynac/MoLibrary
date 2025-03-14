using System.Collections;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json.Serialization;
using BuildingBlocksPlatform.Extensions;
using Microsoft.AspNetCore.Http;
using System.Globalization;
using System;
using System.Text.Encodings.Web;
using BuildingBlocksPlatform.DataSync.Interfaces;
using BuildingBlocksPlatform.SeedWork;
using TimeExtensions = BuildingBlocksPlatform.Extensions.TimeExtensions;

namespace BuildingBlocksPlatform.Converters;

public interface IGlobalJsonOption
{
    /// <summary>
    /// 全局唯一Json序列化设置
    /// </summary>
    public JsonSerializerOptions GlobalOptions { get; }
}

public class JsonShared : IGlobalJsonOption
{
    /// <summary>
    /// 全局的Json设置。用于Mvc等
    /// </summary>
    internal static JsonSerializerOptions GlobalJsonSerializerOptions { get; set; } = new();

    internal class Options
    {
        public static bool WhenWritingNull { get; set; }
        public static bool ReferenceHandlerPreserve { get; set; }
    }


    ///// <summary>
    ///// 全局的后端Json设置。用于领域事件推送等。
    ///// </summary>
    //internal static JsonSerializerOptions GlobalBackendJsonSerializerOptions { get; set; } = new()
    //{
    //    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
    //    PropertyNameCaseInsensitive = true,
    //    Converters = { new DateTimeJsonConverter(), new NullableDateTimeJsonConverter()}
    //};
    internal static readonly string[] DateTimeFormats =
    [
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-dd",
        "yyyy-MM-ddTHH:mm:ss"
    ];
    internal static readonly string OutputDateTimeFormat = "yyyy-MM-dd HH:mm:ss";

    //.NET 6后 TimeZoneInfo的ID 支持跨平台自动转换
    internal static TimeZoneInfo CurTimeZoneInfo => TimeExtensions.LocalTimeZoneInfo;

    /// <summary>
    /// 统一标准化从外部传入的时间
    /// 巨坑：2024-08-08T03:27:05+08:00格式的 MVC序列化会自动转化为Kind为UTC的DateTime。
    /// </summary>
    /// <param name="dateTime"></param>
    /// <returns></returns>
    internal static DateTime NormalizeInTime(DateTime dateTime)
    {
        return dateTime;
        //switch (dateTime.Kind)
        //{
        //    case DateTimeKind.Utc:
        //        return dateTime;
        //    case DateTimeKind.Local:
        //        return dateTime.ToUniversalTime();
        //}

        //return TimeZoneInfo.ConvertTimeToUtc(dateTime, CurTimeZoneInfo);
    }

    /// <summary>
    /// 统一标准化输出给外部的时间
    /// </summary>
    /// <param name="dateTime"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    internal static DateTime NormalizeOutTime(DateTime dateTime)
    {
        return dateTime;
        //try
        //{
        //    return TimeZoneInfo.ConvertTimeFromUtc(dateTime, CurTimeZoneInfo);
        //}
        //catch (Exception e)
        //{
        //    throw new Exception("代码不要使用DateTime.Now等会使得DateTime Kind变为Local的方法，会使得后端混乱，后端统一使用UTC", e);
        //}
    }

    public JsonSerializerOptions GlobalOptions => GlobalJsonSerializerOptions;
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
   
    public static void AddSharedJsonConverter(this IServiceCollection services, bool whenWritingNull = false, bool referenceHandlerPreserve =false)
    {
        JsonShared.Options.WhenWritingNull = whenWritingNull;
        JsonShared.Options.ReferenceHandlerPreserve = referenceHandlerPreserve;
        var options = new JsonSerializerOptions();
        options.ConfigGlobalJsonSerializeOptions([typeof(ResponseCode), typeof(ESystemDataSpecialFlags)]);
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

    public static void ConfigGlobalJsonSerializeOptions(this JsonSerializerOptions options, List<Type>? enumTypeToIgnore = null)
    {
        options.Converters.Add(new NullableDateTimeJsonConverter ());
        options.Converters.Add(new DateTimeJsonConverter ());
        //options.Converters.Add(new StringJsonConverter { HttpContextAccessor = httpContextAccessor });
        //options.Converters.Add(new JsonConverterFactoryForICollection { HttpContextAccessor = httpContextAccessor });
        options.Converters.Add(new NullableGuidJsonConverter());
        //options.Converters.Add(new JsonConverterFactoryForDtoObjectClass { HttpContextAccessor = httpContextAccessor });
        options.Converters.Add(new NullableLongToStringJsonConverter());
        options.Converters.Add(new OutJsonConverterFactory(new JsonStringEnumConverter(),
            [.. enumTypeToIgnore ?? []])); //全局枚举对String、int转换支持
        //options.Converters.Add(new JsonStringEnumConverter()); //全局枚举对String、int转换支持


        if (JsonShared.Options.WhenWritingNull)
        {
            options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        }

        if (JsonShared.Options.ReferenceHandlerPreserve)
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



internal interface IHasHttpContextAccessor
{
    internal IHttpContextAccessor? HttpContextAccessor { get; set; }
}

internal interface IJudgeBackendInvoke : IHasHttpContextAccessor
{
    internal const string X_BACKEND_INVOKE = "X-Backend-Invoke";
    /// <summary>
    /// 用于区分前后端调用。
    /// </summary>
    /// <returns></returns>
    internal bool IsBackendInvoke()
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
