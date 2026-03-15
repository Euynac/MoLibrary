using Monica.Core.Features.MoDiffHighlight.Models;

namespace Monica.Core.Features.MoDiffHighlight.Algorithms;

/// <summary>
/// Optimized Myers diff implementation.
/// Based on Eugene W. Myers' "An O(ND) Difference Algorithm and Its Variations".
/// </summary>
public class OptimizedMyersDiffAlgorithm : IDiffAlgorithm
{
    /// <summary>
    /// Computes line-level differences between two texts.
    /// </summary>
    public List<DiffLine> ComputeDiff(string[] oldLines, string[] newLines, DiffHighlightOptions options)
    {
        var result = new List<DiffLine>();
        var oldLinesNormalized = NormalizeLines(oldLines, options);
        var newLinesNormalized = NormalizeLines(newLines, options);
        
        var editScript = ComputeMyersDiff(oldLinesNormalized, newLinesNormalized);
        result = ConvertEditScriptToDiffLines(editScript, oldLines, newLines);
        
        // Collapse adjacent delete/add pairs into modified lines when requested.
        if (options.Mode is EDiffHighlightMode.Character or EDiffHighlightMode.Mixed)
        {
            AddCharacterDiffs(result, options);
        }
        
        return result;
    }
    
    /// <summary>
    /// Computes character-level differences using a layered strategy.
    /// </summary>
    public List<DiffCharacterRange> ComputeCharacterDiff(string oldText, string newText, DiffHighlightOptions options)
    {
        if (string.IsNullOrEmpty(oldText) && string.IsNullOrEmpty(newText))
            return new List<DiffCharacterRange>();
        
        // Prefer word-level ranges when they produce a useful result.
        var wordDiffs = ComputeWordLevelDiff(oldText, newText, options);
        if (wordDiffs.Any())
            return wordDiffs;
        
        // Fall back to character-level ranges when word matching is not enough.
        return ComputeCharacterLevelDiff(oldText, newText, options);
    }
    
    /// <summary>
    /// Executes the core Myers line-diff algorithm.
    /// </summary>
    private List<EditOperation> ComputeMyersDiff(string[] oldLines, string[] newLines)
    {
        var n = oldLines.Length;
        var m = newLines.Length;
        
        // Handle empty inputs up front.
        if (n == 0 && m == 0)
        {
            return new List<EditOperation>();
        }
        
        if (n == 0)
        {
            var ops = new List<EditOperation>();
            for (int i = 0; i < m; i++)
            {
                ops.Add(new EditOperation { Type = EditType.Insert, OldIndex = -1, NewIndex = i });
            }
            return ops;
        }
        
        if (m == 0)
        {
            var ops = new List<EditOperation>();
            for (int i = 0; i < n; i++)
            {
                ops.Add(new EditOperation { Type = EditType.Delete, OldIndex = i, NewIndex = -1 });
            }
            return ops;
        }
        
        var max = n + m;
        var v = new Dictionary<int, int>();
        var trace = new List<Dictionary<int, int>>();
        
        v[1] = 0;
        
        for (var d = 0; d <= max; d++)
        {
            trace.Add(new Dictionary<int, int>(v));
            
            for (var k = -d; k <= d; k += 2)
            {
                int x;
                if (k == -d || (k != d && v.GetValueOrDefault(k - 1, -1) < v.GetValueOrDefault(k + 1, -1)))
                {
                    x = v.GetValueOrDefault(k + 1, -1);
                }
                else
                {
                    x = v.GetValueOrDefault(k - 1, -1) + 1;
                }
                
                var y = x - k;
                
                while (x < n && y < m && oldLines[x].Equals(newLines[y]))
                {
                    x++;
                    y++;
                }
                
                v[k] = x;
                
                if (x >= n && y >= m)
                {
                    return BuildEditScript(trace, oldLines, newLines, d);
                }
            }
        }
        
        return new List<EditOperation>();
    }
    
    /// <summary>
    /// Reconstructs the edit script from the recorded search trace.
    /// </summary>
    private List<EditOperation> BuildEditScript(List<Dictionary<int, int>> trace, string[] oldLines, string[] newLines, int d)
    {
        var operations = new List<EditOperation>();
        var x = oldLines.Length;
        var y = newLines.Length;
        
        for (var depth = d; depth >= 0; depth--)
        {
            var v = trace[depth];
            var k = x - y;
            
            if (depth == 0)
            {
                // Walk the leading diagonal when the trace reaches the origin.
                while (x > 0 && y > 0)
                {
                    operations.Insert(0, new EditOperation { Type = EditType.Equal, OldIndex = x - 1, NewIndex = y - 1 });
                    x--;
                    y--;
                }
                break;
            }
            
            var prevV = trace[depth - 1];
            
            int prevK;
            if (k == -depth || (k != depth && prevV.GetValueOrDefault(k - 1, -1) < prevV.GetValueOrDefault(k + 1, -1)))
            {
                prevK = k + 1;
            }
            else
            {
                prevK = k - 1;
            }
            
            var prevX = prevV.GetValueOrDefault(prevK, -1);
            var prevY = prevX - prevK;
            
            // Consume diagonal moves first because they represent matching items.
            while (x > prevX && y > prevY && x > 0 && y > 0 && oldLines[x - 1].Equals(newLines[y - 1]))
            {
                operations.Insert(0, new EditOperation { Type = EditType.Equal, OldIndex = x - 1, NewIndex = y - 1 });
                x--;
                y--;
            }
            
            // Then record the edit operation that moved into this diagonal.
            if (prevK == k + 1)
            {
                // Moving down means the new text inserted an item.
                operations.Insert(0, new EditOperation { Type = EditType.Insert, OldIndex = -1, NewIndex = y - 1 });
                y--;
            }
            else
            {
                // Moving right means the old text deleted an item.
                operations.Insert(0, new EditOperation { Type = EditType.Delete, OldIndex = x - 1, NewIndex = -1 });
                x--;
            }
        }
        
        return operations;
    }
    
    /// <summary>
    /// Converts the edit script into <see cref="DiffLine"/> entries.
    /// </summary>
    private List<DiffLine> ConvertEditScriptToDiffLines(List<EditOperation> operations, string[] oldLines, string[] newLines)
    {
        var result = new List<DiffLine>();
        
        foreach (var op in operations)
        {
            switch (op.Type)
            {
                case EditType.Equal:
                    if (op.OldIndex >= 0 && op.OldIndex < oldLines.Length && 
                        op.NewIndex >= 0 && op.NewIndex < newLines.Length)
                    {
                        result.Add(new DiffLine
                        {
                            Type = EDiffLineType.Unchanged,
                            OldContent = oldLines[op.OldIndex],
                            NewContent = newLines[op.NewIndex],
                            OldLineNumber = op.OldIndex + 1,
                            NewLineNumber = op.NewIndex + 1
                        });
                    }
                    break;
                    
                case EditType.Delete:
                    if (op.OldIndex >= 0 && op.OldIndex < oldLines.Length)
                    {
                        result.Add(new DiffLine
                        {
                            Type = EDiffLineType.Deleted,
                            OldContent = oldLines[op.OldIndex],
                            NewContent = "",
                            OldLineNumber = op.OldIndex + 1,
                            NewLineNumber = 0
                        });
                    }
                    break;
                    
                case EditType.Insert:
                    if (op.NewIndex >= 0 && op.NewIndex < newLines.Length)
                    {
                        result.Add(new DiffLine
                        {
                            Type = EDiffLineType.Added,
                            OldContent = "",
                            NewContent = newLines[op.NewIndex],
                            OldLineNumber = 0,
                            NewLineNumber = op.NewIndex + 1
                        });
                    }
                    break;
            }
        }
        
        return result;
    }
    
    /// <summary>
    /// Computes word-level diff ranges.
    /// </summary>
    private List<DiffCharacterRange> ComputeWordLevelDiff(string oldText, string newText, DiffHighlightOptions options)
    {
        var oldWords = SplitIntoWords(oldText);
        var newWords = SplitIntoWords(newText);
        
        var wordEditScript = ComputeMyersWordDiff(oldWords, newWords);
        return ConvertWordEditScriptToCharacterRanges(wordEditScript, oldText, newText);
    }
    
    /// <summary>
    /// Computes character-level diff ranges.
    /// </summary>
    private List<DiffCharacterRange> ComputeCharacterLevelDiff(string oldText, string newText, DiffHighlightOptions options)
    {
        if (oldText.Length > options.MaxCharacterDiffLength || newText.Length > options.MaxCharacterDiffLength)
        {
            // Long texts use a cheaper prefix/suffix scan to avoid expensive matching.
            return ComputeSimplifiedCharacterDiff(oldText, newText);
        }
        
        var oldChars = oldText.ToCharArray().Select(c => c.ToString()).ToArray();
        var newChars = newText.ToCharArray().Select(c => c.ToString()).ToArray();
        
        var charEditScript = ComputeMyersCharDiff(oldChars, newChars);
        return ConvertCharEditScriptToCharacterRanges(charEditScript, oldText, newText);
    }
    
    /// <summary>
    /// Splits text into word and separator tokens.
    /// </summary>
    private string[] SplitIntoWords(string text)
    {
        var words = new List<string>();
        var currentWord = "";
        var inWord = false;
        
        foreach (var c in text)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
            {
                currentWord += c;
                inWord = true;
            }
            else
            {
                if (inWord && !string.IsNullOrEmpty(currentWord))
                {
                    words.Add(currentWord);
                    currentWord = "";
                    inWord = false;
                }
                words.Add(c.ToString());
            }
        }
        
        if (inWord && !string.IsNullOrEmpty(currentWord))
        {
            words.Add(currentWord);
        }
        
        return words.ToArray();
    }
    
    /// <summary>
    /// Runs Myers diff at word granularity.
    /// </summary>
    private List<EditOperation> ComputeMyersWordDiff(string[] oldWords, string[] newWords)
    {
        return ComputeMyersGeneric(oldWords, newWords, (a, b) => a.Equals(b));
    }
    
    /// <summary>
    /// Runs Myers diff at character granularity.
    /// </summary>
    private List<EditOperation> ComputeMyersCharDiff(string[] oldChars, string[] newChars)
    {
        return ComputeMyersGeneric(oldChars, newChars, (a, b) => a.Equals(b));
    }
    
    /// <summary>
    /// Shared Myers implementation for arbitrary token sequences.
    /// </summary>
    private List<EditOperation> ComputeMyersGeneric<T>(T[] oldItems, T[] newItems, Func<T, T, bool> equals)
    {
        var n = oldItems.Length;
        var m = newItems.Length;
        var max = n + m;
        
        var v = new Dictionary<int, int>();
        var trace = new List<Dictionary<int, int>>();
        
        v[1] = 0;
        
        for (var d = 0; d <= max; d++)
        {
            trace.Add(new Dictionary<int, int>(v));
            
            for (var k = -d; k <= d; k += 2)
            {
                int x;
                if (k == -d || (k != d && v.GetValueOrDefault(k - 1, 0) < v.GetValueOrDefault(k + 1, 0)))
                {
                    x = v.GetValueOrDefault(k + 1, 0);
                }
                else
                {
                    x = v.GetValueOrDefault(k - 1, 0) + 1;
                }
                
                var y = x - k;
                
                while (x < n && y < m && equals(oldItems[x], newItems[y]))
                {
                    x++;
                    y++;
                }
                
                v[k] = x;
                
                if (x >= n && y >= m)
                {
                    return BuildGenericEditScript(trace, n, m, d);
                }
            }
        }
        
        return new List<EditOperation>();
    }
    
    /// <summary>
    /// Reconstructs a generic edit script from the recorded trace.
    /// </summary>
    private List<EditOperation> BuildGenericEditScript(List<Dictionary<int, int>> trace, int n, int m, int d)
    {
        var operations = new List<EditOperation>();
        var x = n;
        var y = m;
        
        for (var depth = d; depth > 0; depth--)
        {
            var v = trace[depth];
            var prevV = trace[depth - 1];
            var k = x - y;
            
            int prevK;
            if (k == -depth || (k != depth && prevV.GetValueOrDefault(k - 1, 0) < prevV.GetValueOrDefault(k + 1, 0)))
            {
                prevK = k + 1;
            }
            else
            {
                prevK = k - 1;
            }
            
            var prevX = prevV.GetValueOrDefault(prevK, 0);
            var prevY = prevX - prevK;
            
            while (x > prevX && y > prevY)
            {
                operations.Insert(0, new EditOperation { Type = EditType.Equal, OldIndex = x - 1, NewIndex = y - 1 });
                x--;
                y--;
            }
            
            if (depth > 0)
            {
                if (x > prevX)
                {
                    operations.Insert(0, new EditOperation { Type = EditType.Delete, OldIndex = x - 1, NewIndex = -1 });
                    x--;
                }
                else
                {
                    operations.Insert(0, new EditOperation { Type = EditType.Insert, OldIndex = -1, NewIndex = y - 1 });
                    y--;
                }
            }
        }
        
        return operations;
    }
    
    /// <summary>
    /// Converts word-level edits into character ranges.
    /// </summary>
    private List<DiffCharacterRange> ConvertWordEditScriptToCharacterRanges(List<EditOperation> operations, string oldText, string newText)
    {
        var result = new List<DiffCharacterRange>();
        var oldWords = SplitIntoWords(oldText);
        var newWords = SplitIntoWords(newText);
        
        var oldPos = 0;
        var newPos = 0;
        
        foreach (var op in operations)
        {
            switch (op.Type)
            {
                case EditType.Delete:
                    var deletedWord = oldWords[op.OldIndex];
                    result.Add(new DiffCharacterRange
                    {
                        Type = EDiffLineType.Deleted,
                        Start = oldPos,
                        Length = deletedWord.Length,
                        Content = deletedWord
                    });
                    oldPos += deletedWord.Length;
                    break;
                    
                case EditType.Insert:
                    var insertedWord = newWords[op.NewIndex];
                    result.Add(new DiffCharacterRange
                    {
                        Type = EDiffLineType.Added,
                        Start = newPos,
                        Length = insertedWord.Length,
                        Content = insertedWord
                    });
                    newPos += insertedWord.Length;
                    break;
                    
                case EditType.Equal:
                    oldPos += oldWords[op.OldIndex].Length;
                    newPos += newWords[op.NewIndex].Length;
                    break;
            }
        }
        
        return result;
    }
    
    /// <summary>
    /// Converts character-level edits into character ranges.
    /// </summary>
    private List<DiffCharacterRange> ConvertCharEditScriptToCharacterRanges(List<EditOperation> operations, string oldText, string newText)
    {
        var result = new List<DiffCharacterRange>();
        var oldChars = oldText.ToCharArray();
        var newChars = newText.ToCharArray();
        
        foreach (var op in operations)
        {
            switch (op.Type)
            {
                case EditType.Delete:
                    result.Add(new DiffCharacterRange
                    {
                        Type = EDiffLineType.Deleted,
                        Start = op.OldIndex,
                        Length = 1,
                        Content = oldChars[op.OldIndex].ToString()
                    });
                    break;
                    
                case EditType.Insert:
                    result.Add(new DiffCharacterRange
                    {
                        Type = EDiffLineType.Added,
                        Start = op.NewIndex,
                        Length = 1,
                        Content = newChars[op.NewIndex].ToString()
                    });
                    break;
            }
        }
        
        return result;
    }
    
    /// <summary>
    /// Uses a simplified character diff for long texts.
    /// </summary>
    private List<DiffCharacterRange> ComputeSimplifiedCharacterDiff(string oldText, string newText)
    {
        var result = new List<DiffCharacterRange>();
        
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
        
        // Find the unchanged prefix and suffix first, then mark the middle as changed.
        var prefixLength = 0;
        var suffixLength = 0;
        var minLength = Math.Min(oldText.Length, newText.Length);
        
        while (prefixLength < minLength && oldText[prefixLength] == newText[prefixLength])
        {
            prefixLength++;
        }
        
        while (suffixLength < minLength - prefixLength && 
               oldText[oldText.Length - 1 - suffixLength] == newText[newText.Length - 1 - suffixLength])
        {
            suffixLength++;
        }
        
        if (prefixLength + suffixLength < oldText.Length)
        {
            result.Add(new DiffCharacterRange
            {
                Type = EDiffLineType.Deleted,
                Start = prefixLength,
                Length = oldText.Length - prefixLength - suffixLength,
                Content = oldText.Substring(prefixLength, oldText.Length - prefixLength - suffixLength)
            });
        }
        
        if (prefixLength + suffixLength < newText.Length)
        {
            result.Add(new DiffCharacterRange
            {
                Type = EDiffLineType.Added,
                Start = prefixLength,
                Length = newText.Length - prefixLength - suffixLength,
                Content = newText.Substring(prefixLength, newText.Length - prefixLength - suffixLength)
            });
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
    /// Converts adjacent delete/add pairs into modified lines with character ranges.
    /// </summary>
    private void AddCharacterDiffs(List<DiffLine> lines, DiffHighlightOptions options)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            
            // Treat a deleted line followed by an added line as a potential modification.
            if (line.Type == EDiffLineType.Deleted && 
                i + 1 < lines.Count && 
                lines[i + 1].Type == EDiffLineType.Added)
            {
                var deletedLine = line;
                var addedLine = lines[i + 1];
                
                var charDiffs = ComputeCharacterDiff(deletedLine.OldContent, addedLine.NewContent, options);
                
                if (charDiffs.Any())
                {
                    deletedLine.Type = EDiffLineType.Modified;
                    deletedLine.NewContent = addedLine.NewContent;
                    deletedLine.NewLineNumber = addedLine.NewLineNumber;
                    deletedLine.CharacterDiffs = charDiffs;
                    
                    lines.RemoveAt(i + 1);
                }
            }
        }
    }
}

/// <summary>
/// Represents the type of edit operation in a Myers edit script.
/// </summary>
public enum EditType
{
    Equal,
    Delete,
    Insert
}

/// <summary>
/// Represents a single edit operation in a Myers edit script.
/// </summary>
public class EditOperation
{
    public EditType Type { get; set; }
    public int OldIndex { get; set; }
    public int NewIndex { get; set; }
}
