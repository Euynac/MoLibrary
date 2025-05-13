using System.Text.Json;
using System.Text.Json.Serialization;
using MoLibrary.Core.Module.Interfaces;

namespace MoLibrary.Core.Modules;

public class ModuleGlobalJsonOption : IMoModuleOption<ModuleGlobalJson>
{

    public Action<JsonSerializerOptions>? ExtendAction { get; set; }

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

    /// <summary>
    /// 启用全局枚举格式化值（通过EnumFormatValue特性）
    /// <para>启用后会自动使用枚举上标记的EnumFormatValueAttribute进行序列化和反序列化</para>
    /// <para>When enabled, enums with the EnumFormatValueAttribute will be serialized to their formatted value</para>
    /// </summary>
    public bool EnableEnumFormatValue { get; set; } 
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

//如果有多个相同类型的Converter，则只会使用前面注册的，ABP也有Guid?，所以需要替换掉。

//DateTime?只会处理DateTime?类型，DateTime类型不会处理。具体是因为CanConvert方法。

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
