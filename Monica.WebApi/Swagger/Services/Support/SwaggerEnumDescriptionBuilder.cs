using System.ComponentModel;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.XPath;
using Monica.WebApi.Swagger.Models;

namespace Monica.WebApi.Swagger.Services.Support;

/// <summary>
/// Builds enum metadata payloads used by Swagger schema filters.
/// </summary>
internal static partial class SwaggerEnumDescriptionBuilder
{
    public static JsonArray GetEnumNamesArray(Type enumType)
    {
        var array = new JsonArray();
        foreach (var name in Enum.GetNames(enumType))
        {
            array.Add(JsonValue.Create(name));
        }

        return array;
    }

    public static JsonArray GetEnumDescriptionsArray(
        Type enumType,
        ESwaggerEnumDescriptionSource descriptionSource,
        IReadOnlyList<XPathNavigator>? xmlNavigators)
    {
        var array = new JsonArray();
        foreach (var name in Enum.GetNames(enumType))
        {
            var description = GetEnumValueDescription(enumType, name, descriptionSource, xmlNavigators);
            array.Add(JsonValue.Create(description ?? name));
        }

        return array;
    }

    public static string BuildEnumValuesDescription(
        Type enumType,
        ESwaggerEnumDescriptionSource descriptionSource,
        IReadOnlyList<XPathNavigator>? xmlNavigators)
    {
        var underlyingType = Enum.GetUnderlyingType(enumType);
        var values = Enum.GetValues(enumType);
        var names = Enum.GetNames(enumType);

        var parts = new List<string>(names.Length);
        for (var index = 0; index < names.Length; index++)
        {
            var name = names[index];
            var rawValue = values.GetValue(index);
            var value = Convert.ChangeType(rawValue, underlyingType);
            var description = GetEnumValueDescription(enumType, name, descriptionSource, xmlNavigators);

            parts.Add(description is not null && description != name
                ? $"{value} = {name} ({description})"
                : $"{value} = {name}");
        }

        return string.Join("\n\n", parts);
    }

    private static string? GetEnumValueDescription(
        Type enumType,
        string valueName,
        ESwaggerEnumDescriptionSource descriptionSource,
        IReadOnlyList<XPathNavigator>? xmlNavigators)
    {
        var field = enumType.GetField(valueName);
        if (field is null)
        {
            return null;
        }

        return descriptionSource switch
        {
            ESwaggerEnumDescriptionSource.DescriptionAttribute => GetDescriptionFromAttribute(field),
            ESwaggerEnumDescriptionSource.XmlComments => GetDescriptionFromXml(enumType, valueName, xmlNavigators),
            ESwaggerEnumDescriptionSource.Both => GetDescriptionFromAttribute(field)
                                                  ?? GetDescriptionFromXml(enumType, valueName, xmlNavigators),
            _ => null
        };
    }

    private static string? GetDescriptionFromAttribute(FieldInfo field)
    {
        return field.GetCustomAttribute<DescriptionAttribute>()?.Description;
    }

    private static string? GetDescriptionFromXml(
        Type enumType,
        string valueName,
        IReadOnlyList<XPathNavigator>? xmlNavigators)
    {
        if (xmlNavigators is null || xmlNavigators.Count == 0)
        {
            return null;
        }

        var memberName = $"F:{enumType.FullName}.{valueName}";
        foreach (var navigator in xmlNavigators)
        {
            var summaryNode = navigator.SelectSingleNode($"/doc/members/member[@name='{memberName}']/summary");
            var summaryText = summaryNode?.InnerXml?.Trim();
            if (string.IsNullOrWhiteSpace(summaryText))
            {
                continue;
            }

            return XmlTagRegex().Replace(summaryText, string.Empty).Trim();
        }

        return null;
    }

    [GeneratedRegex("<[^>]+>", RegexOptions.Compiled)]
    private static partial Regex XmlTagRegex();
}
