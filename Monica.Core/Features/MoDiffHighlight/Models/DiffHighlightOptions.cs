namespace Monica.Core.Features.MoDiffHighlight.Models;

/// <summary>
/// Configuration options for diff highlighting.
/// </summary>
public class DiffHighlightOptions
{
    /// <summary>
    /// Gets or sets the comparison mode.
    /// </summary>
    public EDiffHighlightMode Mode { get; set; } = EDiffHighlightMode.Line;
    
    /// <summary>
    /// Gets or sets the output format.
    /// </summary>
    public EDiffOutputFormat OutputFormat { get; set; } = EDiffOutputFormat.Html;
    
    /// <summary>
    /// Gets or sets a value indicating whether whitespace should be ignored.
    /// </summary>
    public bool IgnoreWhitespace { get; set; } = false;
    
    /// <summary>
    /// Gets or sets a value indicating whether casing should be ignored.
    /// </summary>
    public bool IgnoreCase { get; set; } = false;
    
    /// <summary>
    /// Gets or sets how many surrounding context lines to keep near changes.
    /// </summary>
    public int ContextLines { get; set; } = 3;
    
    /// <summary>
    /// Gets or sets the maximum line length eligible for character-level diffing.
    /// </summary>
    public int MaxCharacterDiffLength { get; set; } = 1000;
    
    /// <summary>
    /// Gets or sets the optional custom style configuration.
    /// </summary>
    public DiffHighlightStyle? Style { get; set; }
}
