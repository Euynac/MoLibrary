namespace Monica.Repository.Persistence.Models;

/// <summary>
/// Represents a materialized page of repository query results.
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
/// <param name="Items">The items in the current page.</param>
/// <param name="TotalCount">The total number of rows before paging.</param>
/// <param name="Page">The one-based page number.</param>
/// <param name="PageSize">The requested page size.</param>
public sealed record PagedList<T>(IReadOnlyList<T> Items, long TotalCount, int Page, int PageSize)
{
    /// <summary>
    /// Gets the total number of pages for the current <see cref="PageSize"/>.
    /// </summary>
    public int PageCount => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
