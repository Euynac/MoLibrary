using System.ComponentModel.DataAnnotations;

namespace Monica.DomainDrivenDesign.AutoCrud.Interfaces;

/// <summary>
/// Indicates that the request supports paging.
/// </summary>
public interface IHasRequestPage : IHasRequestSkipCount
{
    /// <summary>
    /// Current page number.
    /// </summary>
    public int? Page { get; set; }

    /// <summary>
    /// Disables paging and returns all data.
    /// </summary>
    public bool? DisablePage { get; set; }
}


/// <summary>
/// Indicates that the request limits the maximum number of returned items.
/// </summary>
public interface IHasRequestLimitedResult
{
    /// <summary>
    /// Maximum number of items returned per response. In paged queries, this is the page size.
    /// </summary>
    [Range(1, 2147483647)]
    public int MaxResultCount { get; set; }
}

/// <summary>
/// Indicates that the request supports skipping a number of items.
/// </summary>
public interface IHasRequestSkipCount : IHasRequestLimitedResult
{
    /// <summary>
    /// Number of items to skip starting from the first item on the first page.
    /// </summary>
    [Range(0, 2147483647)]
    public int? SkipCount { get; set; }
}

/// <summary>
/// Indicates that the request supports keyset pagination.
/// </summary>
public interface IHasRequestKeysetPage : IHasRequestLimitedResult
{
    public string? Cursor { get; set; }

    bool HasUsingKeyset()
    {
        return !string.IsNullOrWhiteSpace(Cursor);
    }
}
