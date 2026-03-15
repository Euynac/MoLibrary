namespace Monica.Core.Features.MoDiffHighlight.Models;

/// <summary>
/// Result produced by a diff highlighting operation.
/// </summary>
public class DiffHighlightResult
{
    /// <summary>
    /// Gets or sets the rendered diff content.
    /// </summary>
    public string HighlightedContent { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the aggregated diff statistics.
    /// </summary>
    public DiffStatistics Statistics { get; set; } = new();
    
    /// <summary>
    /// Gets or sets the line-level diff details.
    /// </summary>
    public List<DiffLine> Lines { get; set; } = new();
    
    /// <summary>
    /// Gets or sets the options used for this diff.
    /// </summary>
    public DiffHighlightOptions Options { get; set; } = new();
    
    /// <summary>
    /// Gets or sets the processing time in milliseconds.
    /// </summary>
    public long ProcessingTimeMs { get; set; }
    
    /// <summary>
    /// Gets a value indicating whether the diff contains any changes.
    /// </summary>
    public bool HasChanges => Statistics.TotalChanges > 0;
    
    /// <summary>
    /// Counts the lines of a given diff type.
    /// </summary>
    /// <param name="type">The diff line type to count.</param>
    /// <returns>The number of matching lines.</returns>
    public int GetLineCount(EDiffLineType type)
    {
        return Lines.Count(line => line.Type == type);
    }
}
