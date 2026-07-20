namespace Monica.WebApi.AutoControllers.Models;

/// <summary>
/// Defines immutable paging defaults used by AutoController request models.
/// </summary>
public static class AutoControllerPaginationDefaults
{
    /// <summary>
    /// The default number of results requested when no explicit value is supplied.
    /// </summary>
    public const int DefaultResultCount = 10;

    /// <summary>
    /// The default maximum for a general limited-result request.
    /// </summary>
    public const int MaximumResultCount = 1_000;

    /// <summary>
    /// The default maximum for the built-in CRUD paging request.
    /// </summary>
    public const int MaximumCrudResultCount = 100_000;
}
