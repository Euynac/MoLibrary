using System.Collections;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Monica.Tool.MoResponse;

namespace Monica.JobScheduler.UI.Services;

/// <summary>
/// Service for generating default JSON values and validating JSON against CLR types.
/// </summary>
public class JobArgsSchemaService
{
    private const int MaxRecursionDepth = 3;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Generates a default JSON string based on the provided CLR type.
    /// </summary>
    /// <param name="argsType">The CLR type to generate JSON for.</param>
    /// <returns>A result containing the generated JSON string or an error.</returns>
    public Res<string> GenerateDefaultJson(Type? argsType)
    {
        if (argsType == null)
        {
            return Res.Ok<string>("null");
        }

        try
        {
            var visitedTypes = new HashSet<Type>();
            var defaultValue = CreateDefaultInstance(argsType, 0, visitedTypes);
            var json = JsonSerializer.Serialize(defaultValue, SerializerOptions);
            return Res.Ok<string>(json);
        }
        catch (Exception ex)
        {
            return Res.Fail($"生成默认 JSON 失败: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates a JSON string and deserializes it to the target type.
    /// </summary>
    /// <param name="json">The JSON string to validate.</param>
    /// <param name="targetType">The target CLR type for deserialization.</param>
    /// <returns>A result containing the deserialized object or an error.</returns>
    public Res<object?> ValidateAndDeserialize(string json, Type? targetType)
    {
        if (targetType == null)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Trim() == "null")
            {
                return Res.Ok<object?>(null);
            }
            return Res.Fail("此作业不接受参数");
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            return Res.Fail("JSON 不能为空");
        }

        try
        {
            var result = JsonSerializer.Deserialize(json, targetType, SerializerOptions);
            return Res.Ok(result);
        }
        catch (JsonException ex)
        {
            return Res.Fail($"JSON 反序列化失败: {ex.Message}");
        }
    }

    /// <summary>
    /// Generates a type reference document showing the properties of the type.
    /// </summary>
    /// <param name="argsType">The CLR type to generate reference for.</param>
    /// <returns>A formatted string showing the type's properties.</returns>
    public string GenerateTypeReference(Type? argsType)
    {
        if (argsType == null)
        {
            return "无参数类型";
        }

        var sb = new StringBuilder();
        GenerateTypeReferenceRecursive(argsType, sb, 0, new HashSet<Type>());
        return sb.ToString();
    }

    private void GenerateTypeReferenceRecursive(Type type, StringBuilder sb, int indent, HashSet<Type> visitedTypes)
    {
        var indentStr = new string(' ', indent * 2);

        // Handle nullable types
        var underlyingType = Nullable.GetUnderlyingType(type);
        if (underlyingType != null)
        {
            type = underlyingType;
        }

        // Check for circular reference
        if (!type.IsPrimitive && type != typeof(string) && type != typeof(decimal) &&
            type != typeof(DateTime) && type != typeof(DateTimeOffset) &&
            type != typeof(TimeSpan) && type != typeof(Guid))
        {
            if (visitedTypes.Contains(type))
            {
                sb.AppendLine($"{indentStr}(循环引用)");
                return;
            }
            visitedTypes.Add(type);
        }

        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite)
            .ToList();

        foreach (var prop in properties)
        {
            var propType = prop.PropertyType;
            var typeName = GetFriendlyTypeName(propType);
            var nullable = Nullable.GetUnderlyingType(propType) != null ? "?" : "";

            sb.AppendLine($"{indentStr}{prop.Name}: {typeName}{nullable}");

            // For complex types, show nested properties
            if (IsComplexType(propType) && indent < 2)
            {
                var actualType = Nullable.GetUnderlyingType(propType) ?? propType;
                if (!actualType.IsArray && !IsCollectionType(actualType))
                {
                    GenerateTypeReferenceRecursive(actualType, sb, indent + 1, visitedTypes);
                }
            }
        }

        if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal) ||
            type == typeof(DateTime) || type == typeof(DateTimeOffset) ||
            type == typeof(TimeSpan) || type == typeof(Guid))
        {
            return;
        }
        visitedTypes.Remove(type);
    }

    private object? CreateDefaultInstance(Type type, int depth, HashSet<Type> visitedTypes)
    {
        // Check recursion depth
        if (depth > MaxRecursionDepth)
        {
            return null;
        }

        // Handle nullable types
        var underlyingType = Nullable.GetUnderlyingType(type);
        if (underlyingType != null)
        {
            return CreateDefaultInstance(underlyingType, depth, visitedTypes);
        }

        // Primitive and common types
        if (type == typeof(string)) return "";
        if (type == typeof(int) || type == typeof(int?)) return 0;
        if (type == typeof(long) || type == typeof(long?)) return 0L;
        if (type == typeof(short) || type == typeof(short?)) return (short)0;
        if (type == typeof(byte) || type == typeof(byte?)) return (byte)0;
        if (type == typeof(double) || type == typeof(double?)) return 0.0;
        if (type == typeof(float) || type == typeof(float?)) return 0.0f;
        if (type == typeof(decimal) || type == typeof(decimal?)) return 0m;
        if (type == typeof(bool) || type == typeof(bool?)) return false;
        if (type == typeof(DateTime) || type == typeof(DateTime?)) return DateTime.Now;
        if (type == typeof(DateTimeOffset) || type == typeof(DateTimeOffset?)) return DateTimeOffset.Now;
        if (type == typeof(TimeSpan) || type == typeof(TimeSpan?)) return TimeSpan.Zero;
        if (type == typeof(Guid) || type == typeof(Guid?)) return Guid.Empty;

        // Check for circular reference
        if (visitedTypes.Contains(type))
        {
            return null;
        }
        visitedTypes.Add(type);

        try
        {
            // Arrays
            if (type.IsArray)
            {
                var elementType = type.GetElementType()!;
                return Array.CreateInstance(elementType, 0);
            }

            // Generic collections
            if (type.IsGenericType)
            {
                var genericDef = type.GetGenericTypeDefinition();

                if (genericDef == typeof(List<>) || genericDef == typeof(IList<>) ||
                    genericDef == typeof(ICollection<>) || genericDef == typeof(IEnumerable<>))
                {
                    var elementType = type.GetGenericArguments()[0];
                    var listType = typeof(List<>).MakeGenericType(elementType);
                    return Activator.CreateInstance(listType);
                }

                if (genericDef == typeof(Dictionary<,>) || genericDef == typeof(IDictionary<,>))
                {
                    var keyType = type.GetGenericArguments()[0];
                    var valueType = type.GetGenericArguments()[1];
                    var dictType = typeof(Dictionary<,>).MakeGenericType(keyType, valueType);
                    return Activator.CreateInstance(dictType);
                }
            }

            // Enums
            if (type.IsEnum)
            {
                var values = Enum.GetValues(type);
                return values.Length > 0 ? values.GetValue(0) : 0;
            }

            // Complex objects
            var instance = CreateObjectInstance(type);
            if (instance != null)
            {
                SetDefaultPropertyValues(instance, depth, visitedTypes);
            }
            return instance;
        }
        finally
        {
            visitedTypes.Remove(type);
        }
    }

    private static object? CreateObjectInstance(Type type)
    {
        try
        {
            // Try parameterless constructor first
            var constructor = type.GetConstructor(Type.EmptyTypes);
            if (constructor != null)
            {
                return Activator.CreateInstance(type);
            }

            // Try non-public parameterless constructor
            constructor = type.GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null, Type.EmptyTypes, null);
            if (constructor != null)
            {
                return Activator.CreateInstance(type, true);
            }

            // Try to find constructor with fewest parameters and use defaults
            var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .OrderBy(c => c.GetParameters().Length)
                .ToList();

            if (constructors.Count > 0)
            {
                var ctor = constructors[0];
                var parameters = ctor.GetParameters()
                    .Select(p => GetDefaultValue(p.ParameterType))
                    .ToArray();
                return ctor.Invoke(parameters);
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private void SetDefaultPropertyValues(object instance, int depth, HashSet<Type> visitedTypes)
    {
        var properties = instance.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite && p.CanRead);

        foreach (var prop in properties)
        {
            try
            {
                var currentValue = prop.GetValue(instance);
                if (currentValue == null || IsDefaultValue(currentValue, prop.PropertyType))
                {
                    var defaultValue = CreateDefaultInstance(prop.PropertyType, depth + 1, visitedTypes);
                    if (defaultValue != null)
                    {
                        prop.SetValue(instance, defaultValue);
                    }
                }
            }
            catch
            {
                // Skip properties that can't be set
            }
        }
    }

    private static object? GetDefaultValue(Type type)
    {
        if (type.IsValueType)
        {
            return Activator.CreateInstance(type);
        }
        if (type == typeof(string))
        {
            return "";
        }
        return null;
    }

    private static bool IsDefaultValue(object value, Type type)
    {
        if (type.IsValueType)
        {
            var defaultValue = Activator.CreateInstance(type);
            return value.Equals(defaultValue);
        }
        return value == null;
    }

    private static string GetFriendlyTypeName(Type type)
    {
        var underlyingType = Nullable.GetUnderlyingType(type);
        if (underlyingType != null)
        {
            return GetFriendlyTypeName(underlyingType);
        }

        if (type == typeof(string)) return "string";
        if (type == typeof(int)) return "int";
        if (type == typeof(long)) return "long";
        if (type == typeof(short)) return "short";
        if (type == typeof(byte)) return "byte";
        if (type == typeof(double)) return "double";
        if (type == typeof(float)) return "float";
        if (type == typeof(decimal)) return "decimal";
        if (type == typeof(bool)) return "bool";
        if (type == typeof(DateTime)) return "DateTime";
        if (type == typeof(DateTimeOffset)) return "DateTimeOffset";
        if (type == typeof(TimeSpan)) return "TimeSpan";
        if (type == typeof(Guid)) return "Guid";
        if (type == typeof(object)) return "object";

        if (type.IsArray)
        {
            return $"{GetFriendlyTypeName(type.GetElementType()!)}[]";
        }

        if (type.IsGenericType)
        {
            var genericDef = type.GetGenericTypeDefinition();
            var genericArgs = string.Join(", ", type.GetGenericArguments().Select(GetFriendlyTypeName));

            if (genericDef == typeof(List<>)) return $"List<{genericArgs}>";
            if (genericDef == typeof(IList<>)) return $"IList<{genericArgs}>";
            if (genericDef == typeof(ICollection<>)) return $"ICollection<{genericArgs}>";
            if (genericDef == typeof(IEnumerable<>)) return $"IEnumerable<{genericArgs}>";
            if (genericDef == typeof(Dictionary<,>)) return $"Dictionary<{genericArgs}>";
            if (genericDef == typeof(IDictionary<,>)) return $"IDictionary<{genericArgs}>";

            return $"{type.Name.Split('`')[0]}<{genericArgs}>";
        }

        if (type.IsEnum)
        {
            return $"enum {type.Name}";
        }

        return type.Name;
    }

    private static bool IsComplexType(Type type)
    {
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

        if (underlyingType.IsPrimitive) return false;
        if (underlyingType == typeof(string)) return false;
        if (underlyingType == typeof(decimal)) return false;
        if (underlyingType == typeof(DateTime)) return false;
        if (underlyingType == typeof(DateTimeOffset)) return false;
        if (underlyingType == typeof(TimeSpan)) return false;
        if (underlyingType == typeof(Guid)) return false;
        if (underlyingType.IsEnum) return false;

        return true;
    }

    private static bool IsCollectionType(Type type)
    {
        if (type.IsArray) return true;
        if (!type.IsGenericType) return false;

        var genericDef = type.GetGenericTypeDefinition();
        return genericDef == typeof(List<>) ||
               genericDef == typeof(IList<>) ||
               genericDef == typeof(ICollection<>) ||
               genericDef == typeof(IEnumerable<>) ||
               genericDef == typeof(Dictionary<,>) ||
               genericDef == typeof(IDictionary<,>) ||
               typeof(IEnumerable).IsAssignableFrom(type);
    }
}
