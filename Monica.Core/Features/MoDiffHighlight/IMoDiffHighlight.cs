using Monica.Core.Features.MoDiffHighlight.Models;

namespace Monica.Core.Features.MoDiffHighlight;

/// <summary>
/// Defines text diff highlighting operations.
/// </summary>
public interface IMoDiffHighlight
{
    /// <summary>
    /// Generates a highlighted diff result asynchronously.
    /// </summary>
    /// <param name="oldText">The original text.</param>
    /// <param name="newText">The updated text.</param>
    /// <param name="options">The diff options to apply.</param>
    /// <returns>The highlighted diff result.</returns>
    Task<DiffHighlightResult> HighlightAsync(string oldText, string newText, DiffHighlightOptions? options = null);
    
    /// <summary>
    /// Generates a highlighted diff result synchronously.
    /// </summary>
    /// <param name="oldText">The original text.</param>
    /// <param name="newText">The updated text.</param>
    /// <param name="options">The diff options to apply.</param>
    /// <returns>The highlighted diff result.</returns>
    DiffHighlightResult Highlight(string oldText, string newText, DiffHighlightOptions? options = null);
    
    /// <summary>
    /// Sets a custom renderer for output generation.
    /// </summary>
    /// <param name="renderer">The renderer instance to use.</param>
    void SetRenderer(IDiffHighlightRenderer renderer);
}
