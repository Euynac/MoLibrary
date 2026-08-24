using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace Monica.Tool.Diagnostics;

public static class DebugJson
{
    /// <summary>
    /// Use <see cref="System.Text.Json.JsonSerializer"/> to serialize given object.
    /// </summary>
    /// <param name="s"></param>
    /// <param name="writeIndented">A value that defines whether JSON should use pretty printing.
    /// <see langword="true" /> if JSON should pretty print on serialization; otherwise, <see langword="false" />. The default is <see langword="false" />.
    /// </param>
    /// <param name="includeFields">default behavior will not serialize field.</param>
    /// <param name="customOptions"></param>
    /// <remarks>Not support serialize <see cref="Exception"/> and <see cref="Type"/>. See <see href="https://github.com/dotnet/runtime/issues/43026"/> and <see href="https://github.com/dotnet/runtime/issues/31567#issuecomment-558335944"/></remarks>
    /// <returns></returns>
    public static string? ToJsonString(this object? s, bool writeIndented = true, bool includeFields = false, JsonSerializerOptions? customOptions = null)
    {
        if (s == null) return null;
        if (customOptions is not null)
        {
            return JsonSerializer.Serialize(s, customOptions);
        }
        var options = new JsonSerializerOptions
        {
            WriteIndented = writeIndented,
            IncludeFields = includeFields
        };

        if (writeIndented)
        {
            options.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All);
        }

        var json = JsonSerializer.Serialize(s, options);
        return json;
    }

    /// <summary>
    /// Force <see cref="System.Text.Json.JsonSerializer"/> to serialize given object when normally encounter exception.
    /// Supports serialization of normally unsupported types like <see cref="Type"/>, <see cref="Exception"/>, delegates, etc.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="s"></param>
    /// <param name="writeIndented"></param>
    /// <param name="relaxedEscaping">unicode character including chinese will not escape</param>
    /// <param name="customOptions"></param>
    /// <remarks><see cref="System.Text.Json.JsonSerializer"/> is not support to serialize <see cref="Exception"/> and <see cref="Type"/>. See <see href="https://github.com/dotnet/runtime/issues/43026"/> and <see href="https://github.com/dotnet/runtime/issues/31567#issuecomment-558335944"/></remarks>
    /// <returns>Not support to deserialize, only use to print Exception or other type info.</returns>
    public static string? ToJsonStringForce<T>(this T? s, bool writeIndented = true, bool relaxedEscaping = true, JsonSerializerOptions? customOptions = null)
    {
        return s.ToJsonStringForce(null, writeIndented, relaxedEscaping, customOptions);
    }

    /// <summary>
    /// Force <see cref="System.Text.Json.JsonSerializer"/> to serialize given object with configurable limits.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="s"></param>
    /// <param name="forceOptions">Options to control serialization limits. If null, defaults are used.</param>
    /// <param name="writeIndented"></param>
    /// <param name="relaxedEscaping"></param>
    /// <param name="customOptions"></param>
    /// <returns></returns>
    public static string? ToJsonStringForce<T>(this T? s, ForceSerializeOptions? forceOptions, bool writeIndented = true, bool relaxedEscaping = true, JsonSerializerOptions? customOptions = null)
    {
        if (s == null) return null;
        forceOptions ??= ForceSerializeOptions.Default;
        forceOptions.Validate();

        var options = customOptions is null
            ? new JsonSerializerOptions { WriteIndented = writeIndented }
            : new JsonSerializerOptions(customOptions);

        if (customOptions is null && relaxedEscaping)
        {
            options.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        }

        options.Converters.Insert(0, new ForceSerializeConverterFactory(forceOptions));
        var json = JsonSerializer.Serialize(s, options);
        return LimitJsonOutput(json, forceOptions.MaxOutputSizeBytes);
    }

    private static string LimitJsonOutput(string json, int maxOutputSizeBytes)
    {
        if (Encoding.UTF8.GetByteCount(json) <= maxOutputSizeBytes)
        {
            return json;
        }

        const string truncationMarker = "{\"$truncated\":\"OUTPUT_SIZE_LIMIT_EXCEEDED\"}";
        if (Encoding.UTF8.GetByteCount(truncationMarker) <= maxOutputSizeBytes)
        {
            return truncationMarker;
        }

        return "null";
    }
}

/// <summary>
/// Options for controlling ForceSerialize behavior to prevent memory issues.
/// </summary>
public class ForceSerializeOptions
{
    /// <summary>
    /// Default options with conservative limits to prevent memory issues.
    /// </summary>
    public static ForceSerializeOptions Default => new();

    /// <summary>
    /// Maximum recursion depth. Default is 10.
    /// </summary>
    public int MaxDepth { get; set; } = 10;

    /// <summary>
    /// Maximum output size in bytes. Serialization stops when exceeded. Default is 1MB.
    /// </summary>
    public int MaxOutputSizeBytes { get; set; } = 1024 * 1024;

    /// <summary>
    /// Maximum number of items to serialize from a collection. Default is 100.
    /// </summary>
    public int MaxCollectionItems { get; set; } = 100;

    /// <summary>
    /// Maximum string length before truncation. Default is 10000.
    /// </summary>
    public int MaxStringLength { get; set; } = 10000;

    /// <summary>
    /// Maximum number of properties to serialize from an object. Default is 50.
    /// </summary>
    public int MaxProperties { get; set; } = 50;

    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegative(MaxDepth);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxOutputSizeBytes, 4);
        ArgumentOutOfRangeException.ThrowIfNegative(MaxCollectionItems);
        ArgumentOutOfRangeException.ThrowIfNegative(MaxStringLength);
        ArgumentOutOfRangeException.ThrowIfNegative(MaxProperties);
    }
}

/// <summary>
/// A JsonConverterFactory that handles serialization of any type, including those not normally supported by System.Text.Json
/// (such as Type, Exception, Delegate, MemberInfo, etc.).
/// </summary>
public class ForceSerializeConverterFactory : JsonConverterFactory
{
    private readonly ForceSerializeOptions _options;

    /// <summary>
    /// Creates a new instance of ForceSerializeConverterFactory.
    /// </summary>
    /// <param name="options">Options to control serialization limits.</param>
    public ForceSerializeConverterFactory(ForceSerializeOptions? options = null)
    {
        _options = options ?? ForceSerializeOptions.Default;
    }
    
    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert) => true;

    /// <inheritdoc />
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        return (JsonConverter)Activator.CreateInstance(
            typeof(ForceSerializeConverter<>).MakeGenericType(typeToConvert),
            _options)!;
    }
}

/// <summary>
/// A smart JsonConverter that attempts normal serialization first, then falls back to property-by-property
/// serialization for complex types, and finally uses ToString() with metadata for types that cannot be serialized.
/// </summary>
/// <typeparam name="T">The type to serialize.</typeparam>
public class ForceSerializeConverter<T> : JsonConverter<T>
{
    private readonly ForceSerializeOptions _options;

    /// <summary>
    /// Creates a new instance of ForceSerializeConverter.
    /// </summary>
    /// <param name="options">Options to control serialization limits.</param>
    public ForceSerializeConverter(ForceSerializeOptions? options = null)
    {
        _options = options ?? ForceSerializeOptions.Default;
    }
    
    /// <inheritdoc />
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotSupportedException("Deserializing with ForceSerializeConverter is not supported. This converter is for debug serialization only.");
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        _options.Validate();
        WriteValue(writer, value, options, new ForceSerializationContext());
    }

    private void WriteValue(
        Utf8JsonWriter writer,
        object? value,
        JsonSerializerOptions options,
        ForceSerializationContext context)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        // Check output size limit
        if (CheckOutputLimitExceeded(writer, context))
        {
            WriteSkippedValue(writer, value.GetType(), "OUTPUT_SIZE_LIMIT_EXCEEDED");
            return;
        }

        // Check depth limit
        if (context.CurrentDepth >= _options.MaxDepth)
        {
            WriteSkippedValue(writer, value.GetType(), "MAX_DEPTH_EXCEEDED");
            return;
        }

        var type = value.GetType();

        // JsonElement needs the same collection, property, string, and depth limits as object graphs.
        if (value is JsonElement jsonElement)
        {
            WriteJsonElement(writer, jsonElement, options, context);
            return;
        }
        
        // Check for types that should be skipped entirely
        if (ShouldSkipType(type))
        {
            WriteSkippedValue(writer, type, "UNSUPPORTED_TYPE");
            return;
        }

        var tracked = !type.IsValueType;
        if (tracked && !context.TryTrack(value))
        {
            WriteSkippedValue(writer, type, "CIRCULAR_REFERENCE");
            return;
        }

        try
        {
            if (TryWriteSpecialType(writer, value, type, options, context))
            {
                return;
            }

            // Only scalar framework values bypass graph traversal. Complex values must honor
            // MaxProperties, MaxCollectionItems, MaxStringLength, and cycle tracking.
            if (IsSimpleValue(type) && TryNormalSerialization(writer, value, type, options))
            {
                return;
            }

            WriteObjectProperties(writer, value, type, options, context);
        }
        finally
        {
            if (tracked)
            {
                context.Untrack(value);
            }
        }
    }

    private bool CheckOutputLimitExceeded(Utf8JsonWriter writer, ForceSerializationContext context)
    {
        if (context.OutputLimitExceeded) return true;

        var currentSize = writer.BytesPending + writer.BytesCommitted;
        if (currentSize > _options.MaxOutputSizeBytes)
        {
            context.MarkOutputLimitExceeded();
            return true;
        }
        return false;
    }

    private static bool ShouldSkipType(Type type)
    {
        // Skip types by namespace - these often have circular references or are not meaningful to serialize
        var ns = type.Namespace;
        if (ns != null)
        {
            if (ns.StartsWith("Microsoft.AspNetCore.", StringComparison.Ordinal)) return true;
            if (ns.StartsWith("Microsoft.Extensions.Primitives", StringComparison.Ordinal)) return true;
            if (ns.StartsWith("System.IO.", StringComparison.Ordinal) && !type.Name.Contains("Exception")) return true;
            if (ns.StartsWith("System.Threading.", StringComparison.Ordinal)) return true;
            if (ns.StartsWith("System.Net.", StringComparison.Ordinal) && !type.Name.Contains("Exception")) return true;
            if (ns.StartsWith("System.Security.", StringComparison.Ordinal)) return true;
            if (ns.StartsWith("System.Runtime.", StringComparison.Ordinal)) return true;
        }

        // Skip specific problematic types
        if (typeof(Stream).IsAssignableFrom(type)) return true;
        if (typeof(Task).IsAssignableFrom(type)) return true;
        if (typeof(CancellationToken) == type) return true;
        if (typeof(CancellationTokenSource).IsAssignableFrom(type)) return true;
        if (typeof(WaitHandle).IsAssignableFrom(type)) return true;
        if (typeof(Delegate).IsAssignableFrom(type) && type != typeof(Delegate)) return false; // Delegate is handled specially
        if (typeof(IServiceProvider).IsAssignableFrom(type)) return true;
        if (typeof(IAsyncDisposable).IsAssignableFrom(type) && !typeof(Exception).IsAssignableFrom(type)) return true;

        return false;
    }

    private bool TryWriteSpecialType(
        Utf8JsonWriter writer,
        object value,
        Type type,
        JsonSerializerOptions options,
        ForceSerializationContext context)
    {
        // Handle Type
        if (value is Type typeValue)
        {
            WriteTypeInfo(writer, typeValue);
            return true;
        }

        // Handle MemberInfo (MethodInfo, PropertyInfo, FieldInfo, etc.)
        if (value is MemberInfo memberInfo)
        {
            WriteMemberInfo(writer, memberInfo);
            return true;
        }

        // Handle Delegate
        if (value is Delegate del)
        {
            WriteDelegateInfo(writer, del);
            return true;
        }

        // Handle Exception
        if (value is Exception ex)
        {
            WriteException(writer, ex, options, context);
            return true;
        }

        // Handle long strings - truncate
        if (value is string str && str.Length > _options.MaxStringLength)
        {
            writer.WriteStringValue(str.Substring(0, _options.MaxStringLength) + $"...[TRUNCATED, total length: {str.Length}]");
            return true;
        }

        // Handle byte arrays specially to avoid huge output
        if (value is byte[] bytes)
        {
            if (bytes.Length > 1000)
            {
                writer.WriteStringValue($"[byte[{bytes.Length}] - too large to display]");
            }
            else
            {
                writer.WriteStringValue(Convert.ToBase64String(bytes));
            }
            return true;
        }

        return false;
    }

    private void WriteJsonElement(
        Utf8JsonWriter writer,
        JsonElement element,
        JsonSerializerOptions options,
        ForceSerializationContext context)
    {
        if (CheckOutputLimitExceeded(writer, context))
        {
            WriteSkippedValue(writer, typeof(JsonElement), "OUTPUT_SIZE_LIMIT_EXCEEDED");
            return;
        }

        if (context.CurrentDepth >= _options.MaxDepth)
        {
            WriteSkippedValue(writer, typeof(JsonElement), "MAX_DEPTH_EXCEEDED");
            return;
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                context.EnterNestedValue();
                try
                {
                    writer.WriteStartObject();
                    var propertyCount = 0;
                    foreach (var property in element.EnumerateObject())
                    {
                        if (propertyCount >= _options.MaxProperties || CheckOutputLimitExceeded(writer, context))
                        {
                            writer.WriteString("$truncated", $"property limit: {_options.MaxProperties}");
                            break;
                        }

                        writer.WritePropertyName(property.Name);
                        WriteJsonElement(writer, property.Value, options, context);
                        propertyCount++;
                    }
                    writer.WriteEndObject();
                }
                finally
                {
                    context.ExitNestedValue();
                }
                return;
            case JsonValueKind.Array:
                context.EnterNestedValue();
                try
                {
                    writer.WriteStartArray();
                    var itemCount = 0;
                    foreach (var item in element.EnumerateArray())
                    {
                        if (itemCount >= _options.MaxCollectionItems || CheckOutputLimitExceeded(writer, context))
                        {
                            writer.WriteStringValue($"...[TRUNCATED, showing first {_options.MaxCollectionItems} items]");
                            break;
                        }

                        WriteJsonElement(writer, item, options, context);
                        itemCount++;
                    }
                    writer.WriteEndArray();
                }
                finally
                {
                    context.ExitNestedValue();
                }
                return;
            case JsonValueKind.String:
                writer.WriteStringValue(TruncateString(element.GetString(), _options.MaxStringLength));
                return;
            case JsonValueKind.Number:
                var number = element.GetRawText();
                if (number.Length <= _options.MaxStringLength)
                {
                    writer.WriteRawValue(number);
                }
                else
                {
                    writer.WriteStringValue(TruncateString(number, _options.MaxStringLength));
                }
                return;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                return;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                return;
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                writer.WriteNullValue();
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(element), element.ValueKind, "Unknown JSON value kind.");
        }
    }

    private static bool IsSimpleValue(Type type)
    {
        return type.IsEnum ||
               Type.GetTypeCode(type) != TypeCode.Object ||
               type == typeof(Guid) ||
               type == typeof(DateTimeOffset) ||
               type == typeof(DateOnly) ||
               type == typeof(TimeOnly) ||
               type == typeof(TimeSpan);
    }

    private static void WriteTypeInfo(Utf8JsonWriter writer, Type type)
    {
        writer.WriteStartObject();
        writer.WriteString("$type", "Type");
        writer.WriteString("Name", type.Name);
        writer.WriteString("FullName", type.FullName);
        writer.WriteBoolean("IsGenericType", type.IsGenericType);
        if (type.IsGenericType)
        {
            writer.WritePropertyName("GenericArguments");
            writer.WriteStartArray();
            foreach (var arg in type.GetGenericArguments())
            {
                writer.WriteStringValue(arg.Name);
            }
            writer.WriteEndArray();
        }
        writer.WriteEndObject();
    }

    private static void WriteMemberInfo(Utf8JsonWriter writer, MemberInfo memberInfo)
    {
        writer.WriteStartObject();
        writer.WriteString("$type", memberInfo.MemberType.ToString());
        writer.WriteString("Name", memberInfo.Name);
        writer.WriteString("DeclaringType", memberInfo.DeclaringType?.FullName);

        if (memberInfo is MethodInfo method)
        {
            writer.WriteString("ReturnType", method.ReturnType.Name);
            writer.WritePropertyName("Parameters");
            writer.WriteStartArray();
            foreach (var param in method.GetParameters())
            {
                writer.WriteStringValue($"{param.ParameterType.Name} {param.Name}");
            }
            writer.WriteEndArray();
        }
        else if (memberInfo is PropertyInfo prop)
        {
            writer.WriteString("PropertyType", prop.PropertyType.Name);
        }
        else if (memberInfo is FieldInfo field)
        {
            writer.WriteString("FieldType", field.FieldType.Name);
        }

        writer.WriteEndObject();
    }

    private static void WriteDelegateInfo(Utf8JsonWriter writer, Delegate del)
    {
        writer.WriteStartObject();
        writer.WriteString("$type", "Delegate");
        writer.WriteString("Type", del.GetType().Name);
        writer.WriteString("Method", del.Method.Name);
        writer.WriteString("Target", del.Target?.GetType().Name ?? "null");
        writer.WriteEndObject();
    }

    private void WriteException(
        Utf8JsonWriter writer,
        Exception ex,
        JsonSerializerOptions options,
        ForceSerializationContext context)
    {
        context.EnterNestedValue();
        try
        {
            if (CheckOutputLimitExceeded(writer, context))
            {
                WriteSkippedValue(writer, ex.GetType(), "OUTPUT_SIZE_LIMIT_EXCEEDED");
                return;
            }

            writer.WriteStartObject();
            writer.WriteString("$type", "Exception");
            writer.WriteString("Type", ex.GetType().FullName);
            writer.WriteString("Message", TruncateString(ex.Message, _options.MaxStringLength));
            writer.WriteString("StackTrace", TruncateString(ex.StackTrace, _options.MaxStringLength));
            writer.WriteString("Source", ex.Source);
            writer.WriteNumber("HResult", ex.HResult);

            if (ex.InnerException != null && context.CurrentDepth < _options.MaxDepth)
            {
                writer.WritePropertyName("InnerException");
                WriteException(writer, ex.InnerException, options, context);
            }

            // Write Data dictionary if it has entries (limited)
            if (ex.Data.Count > 0)
            {
                writer.WritePropertyName("Data");
                writer.WriteStartObject();
                var count = 0;
                foreach (DictionaryEntry entry in ex.Data)
                {
                    if (count++ >= _options.MaxCollectionItems) break;
                    var key = entry.Key?.ToString() ?? "null";
                    writer.WritePropertyName(key);
                    WriteValue(writer, entry.Value, options, context);
                }
                if (ex.Data.Count > _options.MaxCollectionItems)
                {
                    writer.WriteString("$truncated", $"...and {ex.Data.Count - _options.MaxCollectionItems} more entries");
                }
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }
        finally
        {
            context.ExitNestedValue();
        }
    }

    private bool TryNormalSerialization(Utf8JsonWriter writer, object value, Type type, JsonSerializerOptions options)
    {
        // Create options without the ForceSerializeConverterFactory to avoid infinite recursion
        var cleanOptions = new JsonSerializerOptions
        {
            WriteIndented = options.WriteIndented,
            Encoder = options.Encoder,
            DefaultIgnoreCondition = options.DefaultIgnoreCondition,
            PropertyNamingPolicy = options.PropertyNamingPolicy,
            DictionaryKeyPolicy = options.DictionaryKeyPolicy,
            IgnoreReadOnlyProperties = options.IgnoreReadOnlyProperties,
            IgnoreReadOnlyFields = options.IgnoreReadOnlyFields,
            IncludeFields = options.IncludeFields,
            MaxDepth = Math.Min(options.MaxDepth > 0 ? options.MaxDepth : 64, _options.MaxDepth)
        };

        // Copy converters except ForceSerializeConverterFactory
        foreach (var converter in options.Converters)
        {
            if (converter is not ForceSerializeConverterFactory)
            {
                cleanOptions.Converters.Add(converter);
            }
        }

        try
        {
            var json = JsonSerializer.Serialize(value, type, cleanOptions);
            writer.WriteRawValue(json);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void WriteObjectProperties(
        Utf8JsonWriter writer,
        object value,
        Type type,
        JsonSerializerOptions options,
        ForceSerializationContext context)
    {
        context.EnterNestedValue();
        try
        {
            if (CheckOutputLimitExceeded(writer, context))
            {
                WriteSkippedValue(writer, type, "OUTPUT_SIZE_LIMIT_EXCEEDED");
                return;
            }

            // Check if it's an enumerable (but not string or dictionary)
            if (value is IEnumerable enumerable && value is not string && value is not IDictionary)
            {
                WriteEnumerable(writer, enumerable, options, context);
                return;
            }

            // Check if it's a dictionary
            if (value is IDictionary dict)
            {
                WriteDictionary(writer, dict, options, context);
                return;
            }

            // Write as object with properties
            writer.WriteStartObject();

            // Add type hint for debugging
            writer.WriteString("$type", type.Name);

            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                .Take(_options.MaxProperties + 1)
                .ToList();

            foreach (var prop in properties.Take(_options.MaxProperties))
            {
                if (CheckOutputLimitExceeded(writer, context)) break;

                // Skip properties of problematic types
                if (ShouldSkipType(prop.PropertyType))
                {
                    continue;
                }

                object? propValue;
                try
                {
                    propValue = prop.GetValue(value);
                }
                catch (Exception ex)
                {
                    // Property getter threw an exception
                    writer.WritePropertyName(prop.Name);
                    writer.WriteStringValue($"[ERROR: {ex.GetType().Name}: {TruncateString(ex.Message, 200)}]");
                    continue;
                }

                // Skip null values if configured
                if (propValue == null && options.DefaultIgnoreCondition == JsonIgnoreCondition.WhenWritingNull)
                {
                    continue;
                }

                writer.WritePropertyName(prop.Name);
                WriteValue(writer, propValue, options, context);
            }

            if (properties.Count > _options.MaxProperties)
            {
                writer.WriteString("$truncated", $"...and more properties (limit: {_options.MaxProperties})");
            }

            writer.WriteEndObject();
        }
        finally
        {
            context.ExitNestedValue();
        }
    }

    private void WriteEnumerable(
        Utf8JsonWriter writer,
        IEnumerable enumerable,
        JsonSerializerOptions options,
        ForceSerializationContext context)
    {
        writer.WriteStartArray();
        var count = 0;
        foreach (var item in enumerable)
        {
            if (CheckOutputLimitExceeded(writer, context)) break;

            if (count >= _options.MaxCollectionItems)
            {
                writer.WriteStringValue($"...[TRUNCATED, showing first {_options.MaxCollectionItems} items]");
                break;
            }
            WriteValue(writer, item, options, context);
            count++;
        }
        writer.WriteEndArray();
    }

    private void WriteDictionary(
        Utf8JsonWriter writer,
        IDictionary dict,
        JsonSerializerOptions options,
        ForceSerializationContext context)
    {
        writer.WriteStartObject();
        var count = 0;
        foreach (DictionaryEntry entry in dict)
        {
            if (CheckOutputLimitExceeded(writer, context)) break;

            if (count >= _options.MaxCollectionItems)
            {
                writer.WriteString("$truncated", $"...and {dict.Count - count} more entries");
                break;
            }
            var key = entry.Key?.ToString() ?? "null";
            writer.WritePropertyName(key);
            WriteValue(writer, entry.Value, options, context);
            count++;
        }
        writer.WriteEndObject();
    }

    private static void WriteSkippedValue(Utf8JsonWriter writer, Type type, string reason)
    {
        writer.WriteStartObject();
        writer.WriteString("$skipped", reason);
        writer.WriteString("$type", type.FullName ?? type.Name);
        writer.WriteEndObject();
    }

    private static string? TruncateString(string? value, int maxLength)
    {
        if (value == null || value.Length <= maxLength) return value;
        return value.Substring(0, maxLength) + "...[TRUNCATED]";
    }

    private sealed class ForceSerializationContext
    {
        private readonly HashSet<object> _visitedObjects = new(ReferenceEqualityComparer.Instance);

        public int CurrentDepth { get; private set; }

        public bool OutputLimitExceeded { get; private set; }

        public bool TryTrack(object value)
        {
            return _visitedObjects.Add(value);
        }

        public void Untrack(object value)
        {
            _visitedObjects.Remove(value);
        }

        public void EnterNestedValue()
        {
            CurrentDepth++;
        }

        public void ExitNestedValue()
        {
            CurrentDepth--;
        }

        public void MarkOutputLimitExceeded()
        {
            OutputLimitExceeded = true;
        }
    }
}

/// <summary>
/// Comparer that uses reference equality for objects.
/// </summary>
internal sealed class ReferenceEqualityComparer : IEqualityComparer<object>
{
    public static ReferenceEqualityComparer Instance { get; } = new();

    private ReferenceEqualityComparer() { }

    public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

    public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
}
