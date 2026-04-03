namespace Monica.UI.UIDiffHighlight.Models;

/// <summary>
/// Comparison modes supported by the diff highlighter.
/// </summary>
public enum EDiffHighlightMode
{
    /// <summary>
    /// Performs line-level comparison.
    /// </summary>
    Line,
    
    /// <summary>
    /// Performs character-level comparison.
    /// </summary>
    Character,
    
    /// <summary>
    /// Uses a mixed strategy that combines line and character comparison.
    /// </summary>
    Mixed
}
