namespace Monica.Core.Features.MoDiffHighlight.Models;

/// <summary>
/// Display modes for the unified diff view.
/// </summary>
public enum EDiffDisplayMode
{
    /// <summary>
    /// Shows the full comparison.
    /// </summary>
    Compare,
    
    /// <summary>
    /// Shows only the new content.
    /// </summary>
    NewOnly,
    
    /// <summary>
    /// Shows only the original content.
    /// </summary>
    OldOnly
}
