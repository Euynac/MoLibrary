namespace Monica.WebApi.Swagger.Models;

/// <summary>
/// Describes an additional Swagger document exposed by the Monica Swagger module.
/// </summary>
public sealed record SwaggerDocumentDescriptor
{
    /// <summary>
    /// Route segment used by the Swagger document. This value must be unique across all registered Swagger documents.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Display title of the Swagger document. When not set, the document name is used.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Top-level OpenAPI description for the Swagger document.
    /// </summary>
    public string? Description { get; set; }
}
