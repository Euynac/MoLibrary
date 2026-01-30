using System.ComponentModel;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Xml.XPath;

namespace Monica.DomainDrivenDesign.Swagger;

/// <summary>
/// Specifies the source for enum value descriptions.
/// </summary>
public enum EnumDescriptionSource
{
    /// <summary>
    /// Use DescriptionAttribute on enum values.
    /// </summary>
    DescriptionAttribute,

    /// <summary>
    /// Use XML documentation comments.
    /// </summary>
    XmlComments,

    /// <summary>
    /// Use both sources, preferring DescriptionAttribute.
    /// </summary>
    Both
}

/// <summary>
/// Helper methods for enum documentation in Swagger.
/// </summary>
internal static class EnumExtensions
{
    /// <summary>
    /// Gets a JsonArray containing the names of all enum values.
    /// </summary>
    public static JsonArray GetEnumNamesArray(Type enumType)
    {
        var names = Enum.GetNames(enumType);
        var array = new JsonArray();
        foreach (var name in names)
        {
            array.Add(JsonValue.Create(name));
        }
        return array;
    }

    /// <summary>
    /// Gets a JsonArray containing descriptions for all enum values.
    /// </summary>
    public static JsonArray GetEnumDescriptionsArray(
        Type enumType,
        EnumDescriptionSource source,
        IReadOnlyList<XPathNavigator>? xmlNavigators)
    {
        var array = new JsonArray();
        foreach (var name in Enum.GetNames(enumType))
        {
            var description = GetEnumValueDescription(enumType, name, source, xmlNavigators);
            array.Add(JsonValue.Create(description ?? name));
        }
        return array;
    }

    /// <summary>
    /// Builds a human-readable description string for enum values (e.g., "0 = None, 1 = Active").
    /// </summary>
    public static string BuildEnumValuesDescription(
        Type enumType,
        EnumDescriptionSource source,
        IReadOnlyList<XPathNavigator>? xmlNavigators)
    {
        var underlyingType = Enum.GetUnderlyingType(enumType);
        var values = Enum.GetValues(enumType);
        var names = Enum.GetNames(enumType);

        var parts = new List<string>();
        for (var i = 0; i < names.Length; i++)
        {
            var name = names[i];
            var value = Convert.ChangeType(values.GetValue(i), underlyingType);
            var description = GetEnumValueDescription(enumType, name, source, xmlNavigators);

            var part = description != null && description != name
                ? $"{value} = {name} ({description})"
                : $"{value} = {name}";

            parts.Add(part);
        }

        return string.Join("\n\n", parts);
    }

    /// <summary>
    /// Gets the description for a specific enum value.
    /// </summary>
    private static string? GetEnumValueDescription(
        Type enumType,
        string valueName,
        EnumDescriptionSource source,
        IReadOnlyList<XPathNavigator>? xmlNavigators)
    {
        var field = enumType.GetField(valueName);
        if (field == null) return null;

        return source switch
        {
            EnumDescriptionSource.DescriptionAttribute => GetDescriptionFromAttribute(field),
            EnumDescriptionSource.XmlComments => GetDescriptionFromXml(enumType, valueName, xmlNavigators),
            EnumDescriptionSource.Both => GetDescriptionFromAttribute(field)
                                          ?? GetDescriptionFromXml(enumType, valueName, xmlNavigators),
            _ => null
        };
    }

    /// <summary>
    /// Extracts description from DescriptionAttribute.
    /// </summary>
    private static string? GetDescriptionFromAttribute(FieldInfo field)
    {
        var attr = field.GetCustomAttribute<DescriptionAttribute>();
        return attr?.Description;
    }

    /// <summary>
    /// Extracts description from XML documentation.
    /// </summary>
    private static string? GetDescriptionFromXml(
        Type enumType,
        string valueName,
        IReadOnlyList<XPathNavigator>? xmlNavigators)
    {
        if (xmlNavigators == null || xmlNavigators.Count == 0) return null;

        var memberName = $"F:{enumType.FullName}.{valueName}";

        foreach (var navigator in xmlNavigators)
        {
            var node = navigator.SelectSingleNode($"/doc/members/member[@name='{memberName}']/summary");
            if (node != null)
            {
                var summary = node.InnerXml?.Trim();
                if (!string.IsNullOrEmpty(summary))
                {
                    // Remove XML tags and normalize whitespace
                    return System.Text.RegularExpressions.Regex.Replace(summary, @"<[^>]+>", "").Trim();
                }
            }
        }

        return null;
    }
}
