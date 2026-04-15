using System.Xml.XPath;

namespace Monica.WebApi.Swagger.Models;

/// <summary>
/// Options that control how enum metadata is emitted into Swagger schemas.
/// </summary>
public class SwaggerEnumDocumentationOptions
{
    /// <summary>
    /// Determines where enum descriptions are read from. Defaults to <see cref="ESwaggerEnumDescriptionSource.Both"/>.
    /// </summary>
    public ESwaggerEnumDescriptionSource DescriptionSource { get; set; } = ESwaggerEnumDescriptionSource.Both;

    /// <summary>
    /// Controls whether the <c>x-enumDescriptions</c> extension is emitted alongside <c>x-enumNames</c>.
    /// </summary>
    public bool IncludeDescriptions { get; set; } = true;

    /// <summary>
    /// XML documentation navigators used to resolve enum member summaries. Leave null to skip XML-based descriptions.
    /// </summary>
    public IReadOnlyList<XPathNavigator>? XmlNavigators { get; set; }
}
