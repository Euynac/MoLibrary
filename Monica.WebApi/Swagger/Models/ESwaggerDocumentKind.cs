namespace Monica.WebApi.Swagger.Models;

/// <summary>
/// Built-in Swagger document buckets exposed by the Monica Swagger module.
/// </summary>
public enum ESwaggerDocumentKind
{
    /// <summary>
    /// Monica framework and built-in module endpoints.
    /// </summary>
    Monica,

    /// <summary>
    /// Application or business endpoints owned by the host solution.
    /// </summary>
    Business
}
