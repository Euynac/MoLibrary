namespace Monica.UI.UIDiffHighlight.Models;

/// <summary>
/// Represents a single line in a diff result.
/// </summary>
public class DiffLine
{
    /// <summary>
    /// Gets or sets the diff line type.
    /// </summary>
    public EDiffLineType Type { get; set; }
    
    /// <summary>
    /// Gets or sets the original line content.
    /// </summary>
    public string OldContent { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the updated line content.
    /// </summary>
    public string NewContent { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the original line number, where 0 means no source line exists.
    /// </summary>
    public int OldLineNumber { get; set; }
    
    /// <summary>
    /// Gets or sets the updated line number, where 0 means no target line exists.
    /// </summary>
    public int NewLineNumber { get; set; }
    
    /// <summary>
    /// Gets or sets the character-level diff ranges when enabled.
    /// </summary>
    public List<DiffCharacterRange>? CharacterDiffs { get; set; }
}

/// <summary>
/// Represents a character-level diff range.
/// </summary>
public class DiffCharacterRange
{
    /// <summary>
    /// Gets or sets the diff type for this range.
    /// </summary>
    public EDiffLineType Type { get; set; }
    
    /// <summary>
    /// Gets or sets the zero-based start position.
    /// </summary>
    public int Start { get; set; }
    
    /// <summary>
    /// Gets or sets the length of the range.
    /// </summary>
    public int Length { get; set; }
    
    /// <summary>
    /// Gets or sets the affected content.
    /// </summary>
    public string Content { get; set; } = string.Empty;
}
