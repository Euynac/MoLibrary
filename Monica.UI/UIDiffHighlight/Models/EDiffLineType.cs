namespace Monica.UI.UIDiffHighlight.Models;

/// <summary>
/// Line types that can appear in a diff.
/// </summary>
public enum EDiffLineType
{
    /// <summary>
    /// The line is unchanged.
    /// </summary>
    Unchanged,
    
    /// <summary>
    /// The line was added.
    /// </summary>
    Added,
    
    /// <summary>
    /// The line was deleted.
    /// </summary>
    Deleted,
    
    /// <summary>
    /// The line was modified.
    /// </summary>
    Modified
}
