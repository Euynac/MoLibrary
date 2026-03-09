using System.Xml.XPath;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Monica.DomainDrivenDesign.Swagger;

/// <summary>
/// Schema filter that adds x-enumNames and x-enumDescriptions extensions to enum schemas,
/// and enriches descriptions with human-readable value mappings.
/// </summary>
internal class EnumSchemaFilter(
    EnumDescriptionSource descriptionSource,
    IReadOnlyList<XPathNavigator>? xmlNavigators,
    bool includeDescriptions) : ISchemaFilter
{
    private const string XEnumNames = "x-enumNames";
    private const string XEnumDescriptions = "x-enumDescriptions";

    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        // Cast to concrete type for mutation
        if (schema is not OpenApiSchema concrete) return;

        var type = context.Type;

        // Handle nullable enum types
        var enumType = Nullable.GetUnderlyingType(type) ?? type;

        if (!enumType.IsEnum) return;

        // Ensure Extensions dictionary exists
        concrete.Extensions ??= new Dictionary<string, IOpenApiExtension>();

        // Add x-enumNames extension
        var namesArray = EnumExtensions.GetEnumNamesArray(enumType);
        concrete.Extensions[XEnumNames] = new JsonNodeExtension(namesArray);

        // Add x-enumDescriptions extension if requested
        if (includeDescriptions)
        {
            var descriptionsArray = EnumExtensions.GetEnumDescriptionsArray(
                enumType, descriptionSource, xmlNavigators);
            concrete.Extensions[XEnumDescriptions] = new JsonNodeExtension(descriptionsArray);
        }

        // Enrich the schema description with human-readable value mappings
        var enumDescription = EnumExtensions.BuildEnumValuesDescription(
            enumType, descriptionSource, xmlNavigators);

        if (string.IsNullOrEmpty(concrete.Description))
        {
            concrete.Description = enumDescription;
        }
        else if (!concrete.Description.Contains(enumDescription))
        {
            concrete.Description = $"{concrete.Description}\n\n{enumDescription}";
        }
    }
}
