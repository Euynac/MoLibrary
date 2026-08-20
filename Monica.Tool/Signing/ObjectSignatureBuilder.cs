using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using Monica.Tool.Security;

namespace Monica.Tool.Signing;

/// <summary>
/// Builds deterministic signature payloads and hashes from public object properties.
/// </summary>
public static class ObjectSignatureBuilder
{
    /// <summary>
    /// Builds a hash from the instance's serialized signature payload.
    /// </summary>
    public static string BuildHash(
        object instance,
        SignatureOptions? options = null,
        string? supplement = null,
        HashAlgorithmName? algorithm = null)
    {
        ArgumentNullException.ThrowIfNull(instance);

        options ??= new SignatureOptions();
        var payload = BuildPlainText(instance, options) + supplement;
        return Hashing.ComputeHex(payload, algorithm, options.LowercaseHash);
    }

    /// <summary>
    /// Builds the unhashed signature payload for the supplied instance.
    /// </summary>
    public static string BuildPlainText(object instance, SignatureOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(instance);

        options ??= new SignatureOptions();
        var fields = BuildFieldMap(instance, instance.GetType(), options);
        var builder = new StringBuilder();

        switch (options.Mode)
        {
            case SignatureMode.QueryString:
                var normalizedKeys = new HashSet<string>(StringComparer.Ordinal);
                foreach (var (key, value) in fields)
                {
                    var normalizedKey = options.LowercaseKeys ? key.ToLowerInvariant() : key;
                    if (!normalizedKeys.Add(normalizedKey))
                    {
                        throw new InvalidOperationException(
                            $"The signature contains multiple fields that normalize to the key '{normalizedKey}'.");
                    }

                    builder.Append(normalizedKey)
                        .Append('=')
                        .Append(HttpUtility.UrlEncode(value))
                        .Append('&');
                }

                return builder.ToString().TrimEnd('&');
            default:
                throw new ArgumentOutOfRangeException(nameof(options.Mode), options.Mode, "Unsupported signature mode.");
        }
    }

    /// <summary>
    /// Builds a field map using the default signature options and a simple null-value policy.
    /// </summary>
    public static IDictionary<string, string?> BuildFieldMap(object instance, bool ignoreNullValues = true)
    {
        ArgumentNullException.ThrowIfNull(instance);

        return BuildFieldMap(
            instance,
            instance.GetType(),
            new SignatureOptions
            {
                IgnoreNullValues = ignoreNullValues,
            });
    }

    /// <summary>
    /// Builds a field map using the supplied signature options.
    /// </summary>
    public static IDictionary<string, string?> BuildFieldMap(object instance, SignatureOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(instance);
        return BuildFieldMap(instance, instance.GetType(), options ?? new SignatureOptions());
    }

    private static IDictionary<string, string?> BuildFieldMap(
        object instance,
        Type type,
        SignatureOptions options)
    {
        IDictionary<string, string?> fieldMap = options.SortByAscii
            ? new SortedDictionary<string, string?>(StringComparer.Ordinal)
            : new Dictionary<string, string?>(StringComparer.Ordinal);

        var activeObjects = new HashSet<object>(ReferenceEqualityComparer.Instance);
        if (instance is IEnumerable and not string)
        {
            fieldMap.Add("$value", CanonicalizeValue(instance, options, activeObjects));
            return fieldMap;
        }

        AddFields(instance, type, options, fieldMap, activeObjects);
        return fieldMap;
    }

    private static void AddFields(
        object instance,
        Type type,
        SignatureOptions options,
        IDictionary<string, string?> fieldMap,
        HashSet<object> activeObjects)
    {
        var tracked = !type.IsValueType;
        if (tracked && !activeObjects.Add(instance))
        {
            throw new InvalidOperationException(
                $"A reference cycle was detected while building a signature for '{type.FullName}'.");
        }

        try
        {
            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!property.CanRead || property.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                if (ShouldSkip(property, options))
                {
                    continue;
                }

                var propertyName = property.Name;
                if (options.IgnoredPropertyNames.Contains(propertyName))
                {
                    continue;
                }

                var value = property.GetValue(instance);
                if (value == null && options.IgnoreNullValues)
                {
                    continue;
                }

                var valueType = value?.GetType();
                if (value is IEnumerable and not string)
                {
                    AddField(fieldMap, propertyName, CanonicalizeValue(value, options, activeObjects));
                    continue;
                }

                if (valueType is not null && !IsSimpleValue(valueType))
                {
                    AddFields(value!, valueType, options, fieldMap, activeObjects);
                    continue;
                }

                AddField(fieldMap, propertyName, FormatValue(value));
            }
        }
        finally
        {
            if (tracked)
            {
                activeObjects.Remove(instance);
            }
        }
    }

    private static string? FormatValue(object? value) => value switch
    {
        null => null,
        DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
        DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString()
    };

    private static void AddField(IDictionary<string, string?> fieldMap, string name, string? value)
    {
        if (!fieldMap.TryAdd(name, value))
        {
            throw new InvalidOperationException($"Multiple signature properties flatten to the key '{name}'.");
        }
    }

    private static string CanonicalizeValue(
        object? value,
        SignatureOptions options,
        HashSet<object> activeObjects)
    {
        if (value is null)
        {
            return "N";
        }

        var type = value.GetType();
        if (IsSimpleValue(type))
        {
            return CreateToken("S" + (type.FullName ?? type.Name)) + CreateToken(FormatValue(value) ?? string.Empty);
        }

        var tracked = !type.IsValueType;
        if (tracked && !activeObjects.Add(value))
        {
            throw new InvalidOperationException(
                $"A reference cycle was detected while building a signature for '{type.FullName}'.");
        }

        try
        {
            if (value is IDictionary dictionary)
            {
                var entries = dictionary.Keys.Cast<object?>()
                    .Select(key => (
                        Key: CanonicalizeValue(key, options, activeObjects),
                        Value: CanonicalizeValue(dictionary[key!], options, activeObjects)))
                    .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                    .ThenBy(entry => entry.Value, StringComparer.Ordinal);
                return "D" + string.Concat(entries.Select(entry => CreateToken(entry.Key) + CreateToken(entry.Value)));
            }

            if (value is IEnumerable enumerable)
            {
                var builder = new StringBuilder("L");
                foreach (var item in enumerable)
                {
                    builder.Append(CreateToken(CanonicalizeValue(item, options, activeObjects)));
                }
                return builder.ToString();
            }

            var objectBuilder = new StringBuilder("O");
            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                         .Where(property => property.CanRead && property.GetIndexParameters().Length == 0)
                         .Where(property => !ShouldSkip(property, options))
                         .Where(property => !options.IgnoredPropertyNames.Contains(property.Name))
                         .OrderBy(property => property.Name, StringComparer.Ordinal))
            {
                var propertyValue = property.GetValue(value);
                if (propertyValue is null && options.IgnoreNullValues)
                {
                    continue;
                }

                objectBuilder.Append(CreateToken(property.Name));
                objectBuilder.Append(CreateToken(CanonicalizeValue(propertyValue, options, activeObjects)));
            }
            return objectBuilder.ToString();
        }
        finally
        {
            if (tracked)
            {
                activeObjects.Remove(value);
            }
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
               type == typeof(TimeSpan) ||
               type == typeof(Uri);
    }

    private static string CreateToken(string value) => $"{value.Length}:{value}";

    private static bool ShouldSkip(PropertyInfo property, SignatureOptions options)
    {
        if (!options.UseSignatureOnlyAttributes)
        {
            return property.GetCustomAttribute<IgnoreSignatureAttribute>() != null;
        }

        return property.GetCustomAttribute<SignatureOnlyAttribute>() == null;
    }
}
