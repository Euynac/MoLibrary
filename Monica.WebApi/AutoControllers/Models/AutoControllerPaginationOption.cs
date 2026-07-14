namespace Monica.WebApi.AutoControllers.Models;

/// <summary>
/// Configures host-specific limits for AutoController list requests.
/// </summary>
public sealed class AutoControllerPaginationOption
{
    /// <summary>
    /// Gets or sets the result count applied when a request omits <c>MaxResultCount</c>.
    /// The default is 10 and the value must be greater than zero.
    /// </summary>
    public int DefaultResultCount { get; set; } = AutoControllerPaginationDefaults.DefaultResultCount;

    /// <summary>
    /// Gets or sets the largest result count accepted by a general
    /// <see cref="LimitedResultRequestDto" />. The default is 1,000.
    /// </summary>
    public int MaximumResultCount { get; set; } = AutoControllerPaginationDefaults.MaximumResultCount;

    /// <summary>
    /// Gets or sets the largest result count accepted by the built-in
    /// <see cref="CrudPageRequestDto" />. The default is 100,000.
    /// </summary>
    /// <remarks>
    /// Configure a lower value for public or memory-sensitive APIs. This separate limit preserves
    /// the intentionally larger CRUD export/query allowance without widening every limited request.
    /// </remarks>
    public int MaximumCrudResultCount { get; set; } = AutoControllerPaginationDefaults.MaximumCrudResultCount;
}
