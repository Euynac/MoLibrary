using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using Monica.Tool.Extensions;
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
                foreach (var (key, value) in fields)
                {
                    var normalizedKey = options.LowercaseKeys ? key.ToLowerInvariant() : key;
                    builder.Append($"{normalizedKey}={HttpUtility.UrlEncode(value)}&");
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

        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            if (ShouldSkip(property, options))
            {
                continue;
            }

            var value = property.GetValue(instance);
            if (value == null && options.IgnoreNullValues)
            {
                continue;
            }

            var propertyName = property.Name;
            if (options.IgnoredPropertyNames.Contains(propertyName))
            {
                continue;
            }

            if (value != null && property.PropertyType.IsClass && property.PropertyType != typeof(string))
            {
                fieldMap.AddRange(BuildFieldMap(value, property.PropertyType, options));
                continue;
            }

            fieldMap.AddOrReplace(propertyName, value?.ToString());
        }

        return fieldMap;
    }

    private static bool ShouldSkip(PropertyInfo property, SignatureOptions options)
    {
        if (!options.UseSignatureOnlyAttributes)
        {
            return property.GetCustomAttribute<IgnoreSignatureAttribute>() != null;
        }

        return property.GetCustomAttribute<SignatureOnlyAttribute>() == null;
    }
}
