using Monica.Core.Features.MoDiffHighlight.Models;

namespace Monica.Core.Features.MoDiffHighlight.Algorithms;

/// <summary>
/// Simplified diff implementation that uses LCS matching for line comparison.
/// </summary>
public class SimpleMyersDiffAlgorithm : IDiffAlgorithm
{
    /// <summary>
    /// Computes line-level differences between two texts.
    /// </summary>
    public List<DiffLine> ComputeDiff(string[] oldLines, string[] newLines, DiffHighlightOptions options)
    {
        var result = new List<DiffLine>();
        var oldLinesNormalized = NormalizeLines(oldLines, options);
        var newLinesNormalized = NormalizeLines(newLines, options);
        
        var lcs = ComputeLCS(oldLinesNormalized, newLinesNormalized);
        
        int oldIndex = 0;
        int newIndex = 0;
        
        foreach (var (oldPos, newPos) in lcs)
        {
            // Emit deleted lines before the next shared line.
            while (oldIndex < oldPos)
            {
                result.Add(new DiffLine
                {
                    Type = EDiffLineType.Deleted,
                    OldContent = oldLines[oldIndex],
                    NewContent = "",
                    OldLineNumber = oldIndex + 1,
                    NewLineNumber = 0
                });
                oldIndex++;
            }
            
            // Emit added lines before the next shared line.
            while (newIndex < newPos)
            {
                result.Add(new DiffLine
                {
                    Type = EDiffLineType.Added,
                    OldContent = "",
                    NewContent = newLines[newIndex],
                    OldLineNumber = 0,
                    NewLineNumber = newIndex + 1
                });
                newIndex++;
            }
            
            // Emit the shared line once both sequences realign.
            if (oldPos < oldLines.Length && newPos < newLines.Length)
            {
                result.Add(new DiffLine
                {
                    Type = EDiffLineType.Unchanged,
                    OldContent = oldLines[oldPos],
                    NewContent = newLines[newPos],
                    OldLineNumber = oldPos + 1,
                    NewLineNumber = newPos + 1
                });
                oldIndex = oldPos + 1;
                newIndex = newPos + 1;
            }
        }
        
        // Flush trailing deleted lines.
        while (oldIndex < oldLines.Length)
        {
            result.Add(new DiffLine
            {
                Type = EDiffLineType.Deleted,
                OldContent = oldLines[oldIndex],
                NewContent = "",
                OldLineNumber = oldIndex + 1,
                NewLineNumber = 0
            });
            oldIndex++;
        }
        
        // Flush trailing added lines.
        while (newIndex < newLines.Length)
        {
            result.Add(new DiffLine
            {
                Type = EDiffLineType.Added,
                OldContent = "",
                NewContent = newLines[newIndex],
                OldLineNumber = 0,
                NewLineNumber = newIndex + 1
            });
            newIndex++;
        }
        
        // Collapse adjacent delete/add pairs into modified lines when requested.
        if (options.Mode is EDiffHighlightMode.Character or EDiffHighlightMode.Mixed)
        {
            AddCharacterDiffs(result, options);
        }
        
        return result;
    }
    
    /// <summary>
    /// Computes character-level differences between two strings.
    /// </summary>
    public List<DiffCharacterRange> ComputeCharacterDiff(string oldText, string newText, DiffHighlightOptions options)
    {
        var result = new List<DiffCharacterRange>();
        
        if (string.IsNullOrEmpty(oldText) && string.IsNullOrEmpty(newText))
            return result;
        
        if (string.IsNullOrEmpty(oldText))
        {
            result.Add(new DiffCharacterRange
            {
                Type = EDiffLineType.Added,
                Start = 0,
                Length = newText.Length,
                Content = newText
            });
            return result;
        }
        
        if (string.IsNullOrEmpty(newText))
        {
            result.Add(new DiffCharacterRange
            {
                Type = EDiffLineType.Deleted,
                Start = 0,
                Length = oldText.Length,
                Content = oldText
            });
            return result;
        }
        
        // Use an LCS pass over characters to find inserted and deleted spans.
        var oldChars = oldText.ToCharArray();
        var newChars = newText.ToCharArray();
        var charLcs = ComputeCharacterLCS(oldChars, newChars);
        
        int oldPos = 0;
        int newPos = 0;
        
        foreach (var (oldCharPos, newCharPos) in charLcs)
        {
            // Capture deleted character spans from the original text.
            if (oldPos < oldCharPos)
            {
                result.Add(new DiffCharacterRange
                {
                    Type = EDiffLineType.Deleted,
                    Start = oldPos,
                    Length = oldCharPos - oldPos,
                    Content = oldText.Substring(oldPos, oldCharPos - oldPos)
                });
            }
            
            // Capture added character spans from the updated text.
            if (newPos < newCharPos)
            {
                result.Add(new DiffCharacterRange
                {
                    Type = EDiffLineType.Added,
                    Start = newPos,
                    Length = newCharPos - newPos,
                    Content = newText.Substring(newPos, newCharPos - newPos)
                });
            }
            
            oldPos = oldCharPos + 1;
            newPos = newCharPos + 1;
        }
        
        return result;
    }
    
    /// <summary>
    /// Normalizes lines according to whitespace and casing options.
    /// </summary>
    private string[] NormalizeLines(string[] lines, DiffHighlightOptions options)
    {
        return lines.Select(line => NormalizeLine(line, options)).ToArray();
    }
    
    /// <summary>
    /// Normalizes a single line before comparison.
    /// </summary>
    private string NormalizeLine(string line, DiffHighlightOptions options)
    {
        var result = line;
        
        if (options.IgnoreWhitespace)
        {
            result = result.Trim();
        }
        
        if (options.IgnoreCase)
        {
            result = result.ToLowerInvariant();
        }
        
        return result;
    }
    
    /// <summary>
    /// Computes the longest common subsequence (LCS) for two line arrays.
    /// </summary>
    private List<(int, int)> ComputeLCS(string[] oldLines, string[] newLines)
    {
        int m = oldLines.Length;
        int n = newLines.Length;
        var dp = new int[m + 1, n + 1];
        
        // Build the dynamic-programming table for LCS lengths.
        for (int i = 1; i <= m; i++)
        {
            for (int j = 1; j <= n; j++)
            {
                if (oldLines[i - 1].Equals(newLines[j - 1]))
                {
                    dp[i, j] = dp[i - 1, j - 1] + 1;
                }
                else
                {
                    dp[i, j] = Math.Max(dp[i - 1, j], dp[i, j - 1]);
                }
            }
        }
        
        // Backtrack through the table to reconstruct the shared sequence.
        var lcs = new List<(int, int)>();
        int x = m, y = n;
        
        while (x > 0 && y > 0)
        {
            if (oldLines[x - 1].Equals(newLines[y - 1]))
            {
                lcs.Insert(0, (x - 1, y - 1));
                x--;
                y--;
            }
            else if (dp[x - 1, y] > dp[x, y - 1])
            {
                x--;
            }
            else
            {
                y--;
            }
        }
        
        return lcs;
    }
    
    /// <summary>
    /// Computes the longest common subsequence for two character arrays.
    /// </summary>
    private List<(int, int)> ComputeCharacterLCS(char[] oldChars, char[] newChars)
    {
        int m = oldChars.Length;
        int n = newChars.Length;
        
        // Skip quadratic character matching for very long lines.
        if (m > 1000 || n > 1000)
            return new List<(int, int)>();
        
        var dp = new int[m + 1, n + 1];
        
        for (int i = 1; i <= m; i++)
        {
            for (int j = 1; j <= n; j++)
            {
                if (oldChars[i - 1] == newChars[j - 1])
                {
                    dp[i, j] = dp[i - 1, j - 1] + 1;
                }
                else
                {
                    dp[i, j] = Math.Max(dp[i - 1, j], dp[i, j - 1]);
                }
            }
        }
        
        var lcs = new List<(int, int)>();
        int x = m, y = n;
        
        while (x > 0 && y > 0)
        {
            if (oldChars[x - 1] == newChars[y - 1])
            {
                lcs.Insert(0, (x - 1, y - 1));
                x--;
                y--;
            }
            else if (dp[x - 1, y] > dp[x, y - 1])
            {
                x--;
            }
            else
            {
                y--;
            }
        }
        
        return lcs;
    }
    
    /// <summary>
    /// Converts adjacent delete/add pairs into modified lines with character ranges.
    /// </summary>
    private void AddCharacterDiffs(List<DiffLine> lines, DiffHighlightOptions options)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            
            // Treat a deleted line followed by an added line as a potential modification.
            if (line.Type == EDiffLineType.Deleted && 
                i + 1 < lines.Count && 
                lines[i + 1].Type == EDiffLineType.Added)
            {
                var deletedLine = line;
                var addedLine = lines[i + 1];
                
                // Only run character comparison for lines within the configured limit.
                if (deletedLine.OldContent.Length <= options.MaxCharacterDiffLength &&
                    addedLine.NewContent.Length <= options.MaxCharacterDiffLength)
                {
                    var charDiffs = ComputeCharacterDiff(deletedLine.OldContent, addedLine.NewContent, options);
                    
                    // Merge the pair into one modified line when character deltas exist.
                    if (charDiffs.Any())
                    {
                        deletedLine.Type = EDiffLineType.Modified;
                        deletedLine.NewContent = addedLine.NewContent;
                        deletedLine.NewLineNumber = addedLine.NewLineNumber;
                        deletedLine.CharacterDiffs = charDiffs;
                        
                        // Remove the added line because its content is now merged above.
                        lines.RemoveAt(i + 1);
                    }
                }
            }
        }
    }
}
