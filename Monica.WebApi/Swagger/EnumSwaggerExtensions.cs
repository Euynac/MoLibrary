using System.Xml.XPath;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Monica.WebApi.Swagger;

/// <summary>
/// Configuration options for enum documentation in Swagger.
/// </summary>
public class EnumSwaggerOptions
{
    /// <summary>
    /// The source for enum value descriptions. Default is Both.
    /// </summary>
    public EnumDescriptionSource DescriptionSource { get; set; } = EnumDescriptionSource.Both;

    /// <summary>
    /// Whether to include the x-enumDescriptions extension. Default is true.
    /// </summary>
    public bool IncludeDescriptions { get; set; } = true;

    /// <summary>
    /// XML documentation navigators for reading XML comments.
    /// If null, XML comments will not be used for enum descriptions.
    /// </summary>
    public IReadOnlyList<XPathNavigator>? XmlNavigators { get; set; }
}

/// <summary>
/// Extension methods for adding enum documentation to Swagger.
/// </summary>
public static class EnumSwaggerExtensions
{
    /// <summary>
    /// Adds enum documentation support to Swagger, including x-enumNames extension
    /// and human-readable value descriptions.
    /// </summary>
    /// <param name="options">The SwaggerGenOptions to configure.</param>
    /// <param name="configure">Optional configuration action.</param>
    public static void AddEnumDocumentation(
        this SwaggerGenOptions options,
        Action<EnumSwaggerOptions>? configure = null)
    {
        var enumOptions = new EnumSwaggerOptions();
        configure?.Invoke(enumOptions);

        // Add schema filter for x-enumNames extension and description enrichment
        var schemaFilter = new EnumSchemaFilter(
            enumOptions.DescriptionSource,
            enumOptions.XmlNavigators,
            enumOptions.IncludeDescriptions);
        options.AddSchemaFilterInstance(schemaFilter);
    }
}
