using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Text.Unicode;

namespace MoLibrary.Tool.General;

public static class DebugJsonTool
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
        return writeIndented ? Regex.Unescape(json) : json;
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
        return ToJsonStringForce(s, null, writeIndented, relaxedEscaping, customOptions);
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
        if (customOptions is not null)
        {
            return JsonSerializer.Serialize(s, customOptions);
        }

        forceOptions ??= ForceSerializeOptions.Default;

        var options = new JsonSerializerOptions
        {
            WriteIndented = writeIndented
        };

        if (relaxedEscaping)
        {
            options.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        }

        options.Converters.Add(new ForceSerializeConverterFactory(forceOptions));
        var json = JsonSerializer.Serialize(s, options);
        return writeIndented ? Regex.Unescape(json) : json;
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
    public static ForceSerializeOptions Default { get; } = new();

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
    [ThreadStatic]
    private static int _currentDepth;

    [ThreadStatic]
    private static HashSet<object>? _visitedObjects;

    [ThreadStatic]
    private static bool _outputLimitExceeded;

    private readonly ForceSerializeOptions _options;

    /// <summary>
    /// Creates a new instance of ForceSerializeConverter.
    /// </summary>
    /// <param name="options">Options to control serialization limits.</param>
    public ForceSerializeConverter(ForceSerializeOptions? options = null)
    {
        _options = options ?? ForceSerializeOptions.Default;
    }

    /// <summary>
    /// Creates a new instance of ForceSerializeConverter.
    /// </summary>
    /// <param name="maxDepth">Maximum recursion depth.</param>
    [Obsolete("Use ForceSerializeOptions instead")]
    public ForceSerializeConverter(int maxDepth = 32)
    {
        _options = new ForceSerializeOptions { MaxDepth = maxDepth };
    }

    /// <inheritdoc />
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotSupportedException("Deserializing with ForceSerializeConverter is not supported. This converter is for debug serialization only.");
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        // Initialize thread-static state at the root level
        var isRoot = _currentDepth == 0;
        if (isRoot)
        {
            _visitedObjects = new HashSet<object>(ReferenceEqualityComparer.Instance);
            _outputLimitExceeded = false;
        }

        try
        {
            WriteValue(writer, value, options);
        }
        finally
        {
            if (isRoot)
            {
                _visitedObjects?.Clear();
                _visitedObjects = null;
            }
        }
    }

    private void WriteValue(Utf8JsonWriter writer, object? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        // Check output size limit
        if (CheckOutputLimitExceeded(writer))
        {
            WriteSkippedValue(writer, value.GetType(), "OUTPUT_SIZE_LIMIT_EXCEEDED");
            return;
        }

        // Check depth limit
        if (_currentDepth >= _options.MaxDepth)
        {
            WriteSkippedValue(writer, value.GetType(), "MAX_DEPTH_EXCEEDED");
            return;
        }

        var type = value.GetType();

        // Handle JsonElement directly
        if (value is JsonElement jsonElement)
        {
            jsonElement.WriteTo(writer);
            return;
        }

        // Check for types that should be skipped entirely
        if (ShouldSkipType(type))
        {
            WriteSkippedValue(writer, type, "UNSUPPORTED_TYPE");
            return;
        }

        // Check for circular reference (only for reference types)
        if (!type.IsValueType && !TryTrackObject(value))
        {
            WriteSkippedValue(writer, type, "CIRCULAR_REFERENCE");
            return;
        }

        // Handle special types that need custom serialization
        if (TryWriteSpecialType(writer, value, type, options))
        {
            return;
        }

        // Try normal serialization first (without our converter to avoid infinite recursion)
        if (TryNormalSerialization(writer, value, type, options))
        {
            return;
        }

        // Fall back to property-by-property serialization
        WriteObjectProperties(writer, value, type, options);
    }

    private bool CheckOutputLimitExceeded(Utf8JsonWriter writer)
    {
        if (_outputLimitExceeded) return true;

        var currentSize = writer.BytesPending + writer.BytesCommitted;
        if (currentSize > _options.MaxOutputSizeBytes)
        {
            _outputLimitExceeded = true;
            return true;
        }
        return false;
    }

    private bool TryTrackObject(object value)
    {
        _visitedObjects ??= new HashSet<object>(ReferenceEqualityComparer.Instance);
        return _visitedObjects.Add(value);
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

        // Skip compiler-generated types
        if (Attribute.IsDefined(type, typeof(CompilerGeneratedAttribute))) return true;

        // Skip types with names suggesting internal/infrastructure use
        var typeName = type.Name;
        if (typeName.StartsWith("<", StringComparison.Ordinal)) return true; // Anonymous types, closures
        if (typeName.Contains("__")) return true; // Compiler-generated

        return false;
    }

    private bool TryWriteSpecialType(Utf8JsonWriter writer, object value, Type type, JsonSerializerOptions options)
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
            WriteException(writer, ex, options);
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

    private void WriteException(Utf8JsonWriter writer, Exception ex, JsonSerializerOptions options)
    {
        _currentDepth++;
        try
        {
            if (CheckOutputLimitExceeded(writer))
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

            if (ex.InnerException != null && _currentDepth < _options.MaxDepth)
            {
                writer.WritePropertyName("InnerException");
                WriteException(writer, ex.InnerException, options);
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
                    WriteValue(writer, entry.Value, options);
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
            _currentDepth--;
        }
    }

    private bool TryNormalSerialization(Utf8JsonWriter writer, object value, Type type, JsonSerializerOptions options)
    {
        // Skip trying normal serialization for types known to fail
        if (IsKnownProblematicType(type))
        {
            return false;
        }

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

    private static bool IsKnownProblematicType(Type type)
    {
        // Types that are known to fail with System.Text.Json
        if (typeof(Type).IsAssignableFrom(type)) return true;
        if (typeof(MemberInfo).IsAssignableFrom(type)) return true;
        if (typeof(Delegate).IsAssignableFrom(type)) return true;
        if (typeof(Exception).IsAssignableFrom(type)) return true;
        if (typeof(Assembly).IsAssignableFrom(type)) return true;
        if (typeof(Module).IsAssignableFrom(type)) return true;
        if (typeof(IntPtr) == type || typeof(UIntPtr) == type) return true;

        return false;
    }

    private void WriteObjectProperties(Utf8JsonWriter writer, object value, Type type, JsonSerializerOptions options)
    {
        _currentDepth++;
        try
        {
            if (CheckOutputLimitExceeded(writer))
            {
                WriteSkippedValue(writer, type, "OUTPUT_SIZE_LIMIT_EXCEEDED");
                return;
            }

            // Check if it's an enumerable (but not string or dictionary)
            if (value is IEnumerable enumerable && value is not string && value is not IDictionary)
            {
                WriteEnumerable(writer, enumerable, options);
                return;
            }

            // Check if it's a dictionary
            if (value is IDictionary dict)
            {
                WriteDictionary(writer, dict, options);
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

            var propertyCount = 0;
            foreach (var prop in properties.Take(_options.MaxProperties))
            {
                if (CheckOutputLimitExceeded(writer)) break;

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
                WriteValue(writer, propValue, options);
                propertyCount++;
            }

            if (properties.Count > _options.MaxProperties)
            {
                writer.WriteString("$truncated", $"...and more properties (limit: {_options.MaxProperties})");
            }

            writer.WriteEndObject();
        }
        finally
        {
            _currentDepth--;
        }
    }

    private void WriteEnumerable(Utf8JsonWriter writer, IEnumerable enumerable, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        var count = 0;
        foreach (var item in enumerable)
        {
            if (CheckOutputLimitExceeded(writer)) break;

            if (count >= _options.MaxCollectionItems)
            {
                writer.WriteStringValue($"...[TRUNCATED, showing first {_options.MaxCollectionItems} items]");
                break;
            }
            WriteValue(writer, item, options);
            count++;
        }
        writer.WriteEndArray();
    }

    private void WriteDictionary(Utf8JsonWriter writer, IDictionary dict, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        var count = 0;
        foreach (DictionaryEntry entry in dict)
        {
            if (CheckOutputLimitExceeded(writer)) break;

            if (count >= _options.MaxCollectionItems)
            {
                writer.WriteString("$truncated", $"...and {dict.Count - count} more entries");
                break;
            }
            var key = entry.Key?.ToString() ?? "null";
            writer.WritePropertyName(key);
            WriteValue(writer, entry.Value, options);
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
