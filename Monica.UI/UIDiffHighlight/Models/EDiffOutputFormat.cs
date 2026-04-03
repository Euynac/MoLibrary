namespace Monica.UI.UIDiffHighlight.Models;

/// <summary>
/// Output formats supported by the diff renderer.
/// </summary>
public enum EDiffOutputFormat
{
    /// <summary>
    /// HTML output with styling for web display.
    /// </summary>
    Html,
    
    /// <summary>
    /// Markdown output suitable for documents.
    /// </summary>
    Markdown,
    
    /// <summary>
    /// Plain-text output with inline markers.
    /// </summary>
    PlainText
}
