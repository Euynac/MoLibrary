namespace Monica.Core.Features.MoDiffHighlight.Models;

/// <summary>
/// Aggregate statistics for a diff result.
/// </summary>
public class DiffStatistics
{
    /// <summary>
    /// Gets or sets the total number of changes, including adds, deletes, and modifications.
    /// </summary>
    public int TotalChanges { get; set; }
    
    /// <summary>
    /// Gets or sets the number of added lines.
    /// </summary>
    public int AddedLines { get; set; }
    
    /// <summary>
    /// Gets or sets the number of deleted lines.
    /// </summary>
    public int DeletedLines { get; set; }
    
    /// <summary>
    /// Gets or sets the number of modified lines.
    /// </summary>
    public int ModifiedLines { get; set; }
    
    /// <summary>
    /// Gets or sets the number of unchanged lines.
    /// </summary>
    public int UnchangedLines { get; set; }
    
    /// <summary>
    /// Gets or sets the total number of lines in the original text.
    /// </summary>
    public int TotalOldLines { get; set; }
    
    /// <summary>
    /// Gets or sets the total number of lines in the updated text.
    /// </summary>
    public int TotalNewLines { get; set; }
    
    /// <summary>
    /// Gets the similarity percentage in the range 0 to 100.
    /// </summary>
    public double SimilarityPercentage => TotalOldLines == 0 ? 
        (TotalNewLines == 0 ? 100.0 : 0.0) : 
        Math.Round((double)UnchangedLines / Math.Max(TotalOldLines, TotalNewLines) * 100, 2);
}
