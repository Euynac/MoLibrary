using System.Text.Json;
using Monica.Configuration.Exceptions;

namespace Monica.Configuration.Services.Support;

/// <summary>
/// Rejects JSON structures whose property identity is ambiguous under Microsoft configuration's
/// case-insensitive path semantics.
/// </summary>
internal static class ConfigurationJsonStructureValidator
{
    public static void ValidateNoCaseInsensitiveDuplicates(
        string json,
        JsonDocumentOptions options,
        string sourceDisplayName)
    {
        using var document = JsonDocument.Parse(json, options);
        ValidateElement(document.RootElement, sourceDisplayName);
    }

    private static void ValidateElement(JsonElement element, string sourceDisplayName)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                ValidateElement(item, sourceDisplayName);
            }

            return;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in element.EnumerateObject())
        {
            if (!names.Add(property.Name))
            {
                throw new ConfigurationValidationFailedException(
                    $"Configuration source '{sourceDisplayName}' contains duplicate property names in one JSON object when compared case-insensitively.");
            }

            ValidateElement(property.Value, sourceDisplayName);
        }
    }
}
