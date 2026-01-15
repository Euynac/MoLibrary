using System.Collections;
using System.Reflection;
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
            if (s == null) return null;
            if (customOptions is not null)
            {
                return JsonSerializer.Serialize(s, customOptions);
            }
            var options = new JsonSerializerOptions
            {
                WriteIndented = writeIndented
            };

            if (relaxedEscaping)
            {
                options.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
            }

            options.Converters.Add(new ForceSerializeConverterFactory());
            var json = JsonSerializer.Serialize(s, options);
            return writeIndented ? Regex.Unescape(json) : json;
        }
}

/// <summary>
    /// A JsonConverterFactory that handles serialization of any type, including those not normally supported by System.Text.Json
    /// (such as Type, Exception, Delegate, MemberInfo, etc.).
    /// </summary>
    public class ForceSerializeConverterFactory : JsonConverterFactory
    {
        private readonly int _maxDepth;

        /// <summary>
        /// Creates a new instance of ForceSerializeConverterFactory.
        /// </summary>
        /// <param name="maxDepth">Maximum recursion depth to prevent stack overflow on circular references.</param>
        public ForceSerializeConverterFactory(int maxDepth = 32)
        {
            _maxDepth = maxDepth;
        }

        /// <inheritdoc />
        public override bool CanConvert(Type typeToConvert) => true;

        /// <inheritdoc />
        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            return (JsonConverter)Activator.CreateInstance(
                typeof(ForceSerializeConverter<>).MakeGenericType(typeToConvert),
                _maxDepth)!;
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
        // ReSharper disable once StaticMemberInGenericType
        private static int _currentDepth;

        private readonly int _maxDepth;

        /// <summary>
        /// Creates a new instance of ForceSerializeConverter.
        /// </summary>
        /// <param name="maxDepth">Maximum recursion depth.</param>
        public ForceSerializeConverter(int maxDepth = 8)
        {
            _maxDepth = maxDepth;
        }

        /// <inheritdoc />
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            throw new NotSupportedException("Deserializing with ForceSerializeConverter is not supported. This converter is for debug serialization only.");
        }

        /// <inheritdoc />
        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            WriteValue(writer, value, options);
        }

        private void WriteValue(Utf8JsonWriter writer, object? value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            // Check depth limit
            if (_currentDepth >= _maxDepth)
            {
                WriteForcedValue(writer, value, "[MAX_DEPTH_EXCEEDED]");
                return;
            }

            var type = value.GetType();

            // Handle JsonElement directly
            if (value is JsonElement jsonElement)
            {
                jsonElement.WriteTo(writer);
                return;
            }

            // Handle primitives and strings directly
            if (TryWritePrimitive(writer, value, type))
            {
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

        private static bool TryWritePrimitive(Utf8JsonWriter writer, object value, Type type)
        {
            switch (value)
            {
                case string s:
                    writer.WriteStringValue(s);
                    return true;
                case bool b:
                    writer.WriteBooleanValue(b);
                    return true;
                case int i:
                    writer.WriteNumberValue(i);
                    return true;
                case long l:
                    writer.WriteNumberValue(l);
                    return true;
                case double d:
                    writer.WriteNumberValue(d);
                    return true;
                case float f:
                    writer.WriteNumberValue(f);
                    return true;
                case decimal dec:
                    writer.WriteNumberValue(dec);
                    return true;
                case byte by:
                    writer.WriteNumberValue(by);
                    return true;
                case short sh:
                    writer.WriteNumberValue(sh);
                    return true;
                case uint ui:
                    writer.WriteNumberValue(ui);
                    return true;
                case ulong ul:
                    writer.WriteNumberValue(ul);
                    return true;
                case DateTime dt:
                    writer.WriteStringValue(dt.ToString("O"));
                    return true;
                case DateTimeOffset dto:
                    writer.WriteStringValue(dto.ToString("O"));
                    return true;
                case Guid g:
                    writer.WriteStringValue(g.ToString());
                    return true;
                case Enum e:
                    writer.WriteStringValue(e.ToString());
                    return true;
                case char c:
                    writer.WriteStringValue(c.ToString());
                    return true;
                case TimeSpan ts:
                    writer.WriteStringValue(ts.ToString());
                    return true;
#if NET6_0_OR_GREATER
                case DateOnly dateOnly:
                    writer.WriteStringValue(dateOnly.ToString("O"));
                    return true;
                case TimeOnly timeOnly:
                    writer.WriteStringValue(timeOnly.ToString("O"));
                    return true;
#endif
                default:
                    return false;
            }
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

            return false;
        }

        private static void WriteTypeInfo(Utf8JsonWriter writer, Type type)
        {
            writer.WriteStartObject();
            writer.WriteString("$specialType", "Type");
            writer.WriteString("Name", type.Name);
            writer.WriteString("FullName", type.FullName);
            writer.WriteString("Namespace", type.Namespace);
            writer.WriteString("AssemblyQualifiedName", type.AssemblyQualifiedName);
            writer.WriteBoolean("IsGenericType", type.IsGenericType);
            if (type.IsGenericType)
            {
                writer.WritePropertyName("GenericArguments");
                writer.WriteStartArray();
                foreach (var arg in type.GetGenericArguments())
                {
                    writer.WriteStringValue(arg.FullName ?? arg.Name);
                }
                writer.WriteEndArray();
            }
            writer.WriteBoolean("IsArray", type.IsArray);
            writer.WriteBoolean("IsClass", type.IsClass);
            writer.WriteBoolean("IsValueType", type.IsValueType);
            writer.WriteBoolean("IsInterface", type.IsInterface);
            writer.WriteBoolean("IsEnum", type.IsEnum);
            writer.WriteEndObject();
        }

        private static void WriteMemberInfo(Utf8JsonWriter writer, MemberInfo memberInfo)
        {
            writer.WriteStartObject();
            writer.WriteString("$specialType", memberInfo.MemberType.ToString());
            writer.WriteString("Name", memberInfo.Name);
            writer.WriteString("DeclaringType", memberInfo.DeclaringType?.FullName);
            writer.WriteString("MemberType", memberInfo.MemberType.ToString());

            if (memberInfo is MethodInfo method)
            {
                writer.WriteString("ReturnType", method.ReturnType.FullName ?? method.ReturnType.Name);
                writer.WritePropertyName("Parameters");
                writer.WriteStartArray();
                foreach (var param in method.GetParameters())
                {
                    writer.WriteStartObject();
                    writer.WriteString("Name", param.Name);
                    writer.WriteString("Type", param.ParameterType.FullName ?? param.ParameterType.Name);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
            }
            else if (memberInfo is PropertyInfo prop)
            {
                writer.WriteString("PropertyType", prop.PropertyType.FullName ?? prop.PropertyType.Name);
                writer.WriteBoolean("CanRead", prop.CanRead);
                writer.WriteBoolean("CanWrite", prop.CanWrite);
            }
            else if (memberInfo is FieldInfo field)
            {
                writer.WriteString("FieldType", field.FieldType.FullName ?? field.FieldType.Name);
                writer.WriteBoolean("IsStatic", field.IsStatic);
                writer.WriteBoolean("IsPublic", field.IsPublic);
            }

            writer.WriteEndObject();
        }

        private static void WriteDelegateInfo(Utf8JsonWriter writer, Delegate del)
        {
            writer.WriteStartObject();
            writer.WriteString("$specialType", "Delegate");
            writer.WriteString("Type", del.GetType().FullName);
            writer.WriteString("MethodName", del.Method.Name);
            writer.WriteString("MethodDeclaringType", del.Method.DeclaringType?.FullName);
            writer.WriteString("Target", del.Target?.GetType().FullName ?? "null");
            writer.WriteEndObject();
        }

        private void WriteException(Utf8JsonWriter writer, Exception ex, JsonSerializerOptions options)
        {
            _currentDepth++;
            try
            {
                writer.WriteStartObject();
                writer.WriteString("$specialType", "Exception");
                writer.WriteString("Type", ex.GetType().FullName);
                writer.WriteString("Message", ex.Message);
                writer.WriteString("StackTrace", ex.StackTrace);
                writer.WriteString("Source", ex.Source);
                writer.WriteNumber("HResult", ex.HResult);

                if (ex.InnerException != null)
                {
                    writer.WritePropertyName("InnerException");
                    WriteException(writer, ex.InnerException, options);
                }

                // Write Data dictionary if it has entries
                if (ex.Data.Count > 0)
                {
                    writer.WritePropertyName("Data");
                    writer.WriteStartObject();
                    foreach (DictionaryEntry entry in ex.Data)
                    {
                        var key = entry.Key?.ToString() ?? "null";
                        writer.WritePropertyName(key);
                        WriteValue(writer, entry.Value, options);
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
                MaxDepth = options.MaxDepth > 0 ? options.MaxDepth : 16
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

                var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanRead && p.GetIndexParameters().Length == 0);

                foreach (var prop in properties)
                {
                    object? propValue;
                    try
                    {
                        propValue = prop.GetValue(value);
                    }
                    catch (Exception ex)
                    {
                        // Property getter threw an exception
                        writer.WritePropertyName(prop.Name);
                        WriteForcedValue(writer, $"[ERROR: {ex.GetType().Name}: {ex.Message}]", null);
                        continue;
                    }

                    // Skip null values if configured
                    if (propValue == null && options.DefaultIgnoreCondition == JsonIgnoreCondition.WhenWritingNull)
                    {
                        continue;
                    }

                    writer.WritePropertyName(prop.Name);
                    WriteValue(writer, propValue, options);
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
            foreach (var item in enumerable)
            {
                WriteValue(writer, item, options);
            }
            writer.WriteEndArray();
        }

        private void WriteDictionary(Utf8JsonWriter writer, IDictionary dict, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            foreach (DictionaryEntry entry in dict)
            {
                var key = entry.Key?.ToString() ?? "null";
                writer.WritePropertyName(key);
                WriteValue(writer, entry.Value, options);
            }
            writer.WriteEndObject();
        }

        private static void WriteForcedValue(Utf8JsonWriter writer, object value, string? reason)
        {
            writer.WriteStartObject();
            writer.WriteBoolean("$forced", true);
            writer.WriteString("$type", value.GetType().FullName);
            if (reason != null)
            {
                writer.WriteString("$reason", reason);
            }
            writer.WriteString("$value", value.ToString());
            writer.WriteEndObject();
        }
    }