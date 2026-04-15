using System.Xml.XPath;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using Monica.WebApi.Swagger.Models;

namespace Monica.WebApi.Swagger.Services.Support;

/// <summary>
/// Adds enum metadata extensions and enriched descriptions to Swagger schemas.
/// </summary>
internal sealed class SwaggerEnumSchemaFilter(
    ESwaggerEnumDescriptionSource descriptionSource,
    IReadOnlyList<XPathNavigator>? xmlNavigators,
    bool includeDescriptions) : ISchemaFilter
{
    private const string XEnumNames = "x-enumNames";
    private const string XEnumDescriptions = "x-enumDescriptions";

    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema is not OpenApiSchema concreteSchema)
        {
            return;
        }

        var enumType = Nullable.GetUnderlyingType(context.Type) ?? context.Type;
        if (!enumType.IsEnum)
        {
            return;
        }

        concreteSchema.Extensions ??= new Dictionary<string, IOpenApiExtension>();
        concreteSchema.Extensions[XEnumNames] = new JsonNodeExtension(
            SwaggerEnumDescriptionBuilder.GetEnumNamesArray(enumType));

        if (includeDescriptions)
        {
            concreteSchema.Extensions[XEnumDescriptions] = new JsonNodeExtension(
                SwaggerEnumDescriptionBuilder.GetEnumDescriptionsArray(enumType, descriptionSource, xmlNavigators));
        }

        var enumDescription = SwaggerEnumDescriptionBuilder.BuildEnumValuesDescription(
            enumType,
            descriptionSource,
            xmlNavigators);

        if (string.IsNullOrEmpty(concreteSchema.Description))
        {
            concreteSchema.Description = enumDescription;
            return;
        }

        if (!concreteSchema.Description.Contains(enumDescription, StringComparison.Ordinal))
        {
            concreteSchema.Description = $"{concreteSchema.Description}\n\n{enumDescription}";
        }
    }
}
