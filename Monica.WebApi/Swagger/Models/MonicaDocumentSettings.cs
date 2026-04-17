namespace Monica.WebApi.Swagger.Models;

/// <summary>
/// Configures the optional Monica framework Swagger document.
/// </summary>
public sealed class MonicaDocumentSettings
{
    /// <summary>
    /// Controls whether Monica-owned framework endpoints are emitted into a dedicated Swagger document.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Route segment used by the Monica framework document. This value must be unique across all registered Swagger documents.
    /// </summary>
    public string Name { get; set; } = "monica";

    /// <summary>
    /// Display title of the Monica framework document. When not set, the Swagger module uses <c>Monica API</c>.
    /// </summary>
    public string? Title { get; set; } = "Monica API";

    /// <summary>
    /// Top-level OpenAPI description for the Monica framework document.
    /// </summary>
    public string? Description { get; set; }
}
