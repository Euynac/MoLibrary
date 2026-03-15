using Monica.Core.Features.MoDiffHighlight.Models;

namespace Monica.Core.Features.MoDiffHighlight;

/// <summary>
/// Defines a renderer for diff highlight output.
/// </summary>
public interface IDiffHighlightRenderer
{
    /// <summary>
    /// Gets the output format supported by this renderer.
    /// </summary>
    EDiffOutputFormat SupportedFormat { get; }
    
    /// <summary>
    /// Renders diff lines into the renderer's output format.
    /// </summary>
    /// <param name="lines">The diff lines to render.</param>
    /// <param name="style">The style settings to apply.</param>
    /// <returns>The rendered content.</returns>
    string Render(IEnumerable<DiffLine> lines, DiffHighlightStyle style);
    
    /// <summary>
    /// Renders a single diff line.
    /// </summary>
    /// <param name="line">The diff line to render.</param>
    /// <param name="style">The style settings to apply.</param>
    /// <returns>The rendered line content.</returns>
    string RenderLine(DiffLine line, DiffHighlightStyle style);
}
