namespace Monica.Core.Features.MoDiffHighlight.Models;

/// <summary>
/// View modes for presenting diff output.
/// </summary>
public enum EDiffViewMode
{
    /// <summary>
    /// A single-pane mixed view similar to GitHub Desktop.
    /// </summary>
    Unified,
    
    /// <summary>
    /// A two-pane side-by-side view similar to VS Code.
    /// </summary>
    Split
}
