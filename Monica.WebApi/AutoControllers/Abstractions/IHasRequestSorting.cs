namespace Monica.WebApi.AutoControllers.Abstractions;

/// <summary>
/// Indicates that the request supports sorting.
/// </summary>
public interface IHasRequestSorting
{
    /// <summary>
    /// Sorting information.
    /// Should include sorting field and optionally a direction (ASC or DESC)
    /// Can contain more than one field separated by comma (,).
    /// </summary>
    /// <example>
    /// Examples:
    /// "Name"
    /// "Name DESC"
    /// "Name ASC, Age DESC"
    /// </example>
    public string? Sorting { get; set; }
}
