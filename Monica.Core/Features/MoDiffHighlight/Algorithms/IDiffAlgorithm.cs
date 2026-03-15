using Monica.Core.Features.MoDiffHighlight.Models;

namespace Monica.Core.Features.MoDiffHighlight.Algorithms;

/// <summary>
/// Defines the contract for diff algorithms.
/// </summary>
public interface IDiffAlgorithm
{
    /// <summary>
    /// Computes line-level differences between two texts.
    /// </summary>
    /// <param name="oldLines">The original text split into lines.</param>
    /// <param name="newLines">The updated text split into lines.</param>
    /// <param name="options">The diff options to apply.</param>
    /// <returns>The resulting diff lines.</returns>
    List<DiffLine> ComputeDiff(string[] oldLines, string[] newLines, DiffHighlightOptions options);
    
    /// <summary>
    /// Computes character-level differences between two strings.
    /// </summary>
    /// <param name="oldText">The original text.</param>
    /// <param name="newText">The updated text.</param>
    /// <param name="options">The diff options to apply.</param>
    /// <returns>The character diff ranges.</returns>
    List<DiffCharacterRange> ComputeCharacterDiff(string oldText, string newText, DiffHighlightOptions options);
}
