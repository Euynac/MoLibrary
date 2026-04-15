using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.SwaggerGen;
using Monica.WebApi.Swagger.Models;
using Monica.WebApi.Swagger.Services.Support;

namespace Monica.WebApi.Swagger.Extensions;

/// <summary>
/// Extension methods for enriching Swagger enum schemas with names and descriptions.
/// </summary>
public static class SwaggerGenEnumDocumentationExtensions
{
    /// <summary>
    /// Adds enum documentation support to Swagger, including <c>x-enumNames</c> and optional
    /// description metadata derived from attributes or XML comments.
    /// </summary>
    /// <param name="options">The Swagger generator options.</param>
    /// <param name="configure">Optional customization for enum documentation behavior.</param>
    public static void AddEnumDocumentation(
        this SwaggerGenOptions options,
        Action<SwaggerEnumDocumentationOptions>? configure = null)
    {
        var documentationOptions = new SwaggerEnumDocumentationOptions();
        configure?.Invoke(documentationOptions);

        options.AddSchemaFilterInstance(new SwaggerEnumSchemaFilter(
            documentationOptions.DescriptionSource,
            documentationOptions.XmlNavigators,
            documentationOptions.IncludeDescriptions));
    }
}
