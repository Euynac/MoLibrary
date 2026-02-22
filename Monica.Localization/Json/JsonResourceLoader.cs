using System.Reflection;
using System.Text.Json;

namespace Monica.Localization.Json;

public static class JsonResourceLoader
{
    public static Dictionary<string, Dictionary<string, string>> LoadFromAssembly(
        Assembly assembly,
        Type resourceType,
        List<string> supportedCultures)
    {
        var result = new Dictionary<string, Dictionary<string, string>>();
        var resourceName = resourceType.Name;

        foreach (var culture in supportedCultures)
        {
            // Expected pattern: {Namespace}.{ResourceName}.{Culture}.json
            var resourcePath = $"{resourceType.Namespace}.{resourceName}.{culture}.json";

            using var stream = assembly.GetManifestResourceStream(resourcePath);
            if (stream == null) continue;

            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            var flatData = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

            if (flatData != null)
            {
                result[culture] = FlattenKeys(flatData);
            }
        }

        return result;
    }

    private static Dictionary<string, string> FlattenKeys(
        Dictionary<string, object> source,
        string? prefix = null)
    {
        var result = new Dictionary<string, string>();

        foreach (var (key, value) in source)
        {
            var fullKey = prefix == null ? key : $"{prefix}:{key}";

            if (value is JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.Object)
                {
                    var nested = JsonSerializer.Deserialize<Dictionary<string, object>>(element.GetRawText());
                    if (nested != null)
                    {
                        foreach (var (nestedKey, nestedValue) in FlattenKeys(nested, fullKey))
                        {
                            result[nestedKey] = nestedValue;
                        }
                    }
                }
                else if (element.ValueKind == JsonValueKind.String)
                {
                    result[fullKey] = element.GetString() ?? fullKey;
                }
            }
            else if (value is string stringValue)
            {
                result[fullKey] = stringValue;
            }
        }

        return result;
    }
}
