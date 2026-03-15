namespace Monica.Core.Features.MoDiffHighlight.Models;

/// <summary>
/// Style settings for diff highlighting output.
/// </summary>
public class DiffHighlightStyle
{
    /// <summary>
    /// Gets or sets the CSS class or HTML attributes for added lines.
    /// </summary>
    public string AddedLineStyle { get; set; } = "diff-added";
    
    /// <summary>
    /// Gets or sets the CSS class or HTML attributes for deleted lines.
    /// </summary>
    public string DeletedLineStyle { get; set; } = "diff-deleted";
    
    /// <summary>
    /// Gets or sets the CSS class or HTML attributes for modified lines.
    /// </summary>
    public string ModifiedLineStyle { get; set; } = "diff-modified";
    
    /// <summary>
    /// Gets or sets the CSS class or HTML attributes for unchanged lines.
    /// </summary>
    public string UnchangedLineStyle { get; set; } = "diff-unchanged";
    
    /// <summary>
    /// Gets or sets the CSS class or HTML attributes for added characters.
    /// </summary>
    public string AddedCharacterStyle { get; set; } = "diff-added-char";
    
    /// <summary>
    /// Gets or sets the CSS class or HTML attributes for deleted characters.
    /// </summary>
    public string DeletedCharacterStyle { get; set; } = "diff-deleted-char";
    
    /// <summary>
    /// Gets or sets the CSS class or HTML attributes for line numbers.
    /// </summary>
    public string LineNumberStyle { get; set; } = "diff-line-number";
    
    /// <summary>
    /// Gets or sets the CSS class or HTML attributes for the outer container.
    /// </summary>
    public string ContainerStyle { get; set; } = "diff-container";
    
    /// <summary>
    /// Gets or sets a value indicating whether the built-in CSS should be included.
    /// </summary>
    public bool IncludeDefaultCss { get; set; } = true;
}
