using MoLibrary.Core.Features.MoDiffHighlight.Models;

namespace MoLibrary.Core.Features.MoDiffHighlight.Algorithms;

/// <summary>
/// 优化的 Myers 差异算法实现
/// 基于 Eugene W. Myers 的 "An O(ND) Difference Algorithm and Its Variations" 论文
/// </summary>
public class OptimizedMyersDiffAlgorithm : IDiffAlgorithm
{
    /// <summary>
    /// 计算两个文本的差异
    /// </summary>
    public List<DiffLine> ComputeDiff(string[] oldLines, string[] newLines, DiffHighlightOptions options)
    {
        var result = new List<DiffLine>();
        var oldLinesNormalized = NormalizeLines(oldLines, options);
        var newLinesNormalized = NormalizeLines(newLines, options);
        
        var editScript = ComputeMyersDiff(oldLinesNormalized, newLinesNormalized);
        result = ConvertEditScriptToDiffLines(editScript, oldLines, newLines);
        
        // 添加字符级差异（如果启用）
        if (options.Mode is EDiffHighlightMode.Character or EDiffHighlightMode.Mixed)
        {
            AddCharacterDiffs(result, options);
        }
        
        return result;
    }
    
    /// <summary>
    /// 计算字符级差异 - 使用改进的算法
    /// </summary>
    public List<DiffCharacterRange> ComputeCharacterDiff(string oldText, string newText, DiffHighlightOptions options)
    {
        if (string.IsNullOrEmpty(oldText) && string.IsNullOrEmpty(newText))
            return new List<DiffCharacterRange>();
        
        // 优先使用词级差异
        var wordDiffs = ComputeWordLevelDiff(oldText, newText, options);
        if (wordDiffs.Any())
            return wordDiffs;
        
        // 回退到字符级差异
        return ComputeCharacterLevelDiff(oldText, newText, options);
    }
    
    /// <summary>
    /// Myers 算法核心实现
    /// </summary>
    private List<EditOperation> ComputeMyersDiff(string[] oldLines, string[] newLines)
    {
        var n = oldLines.Length;
        var m = newLines.Length;
        
        // 处理空数组的情况
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
    /// 构建编辑脚本
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
                // 处理起始点的对角线
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
            
            // 先处理对角线移动（相等的元素）
            while (x > prevX && y > prevY && x > 0 && y > 0 && oldLines[x - 1].Equals(newLines[y - 1]))
            {
                operations.Insert(0, new EditOperation { Type = EditType.Equal, OldIndex = x - 1, NewIndex = y - 1 });
                x--;
                y--;
            }
            
            // 处理插入或删除
            if (prevK == k + 1)
            {
                // 从上面来的，是插入操作
                operations.Insert(0, new EditOperation { Type = EditType.Insert, OldIndex = -1, NewIndex = y - 1 });
                y--;
            }
            else
            {
                // 从左边来的，是删除操作
                operations.Insert(0, new EditOperation { Type = EditType.Delete, OldIndex = x - 1, NewIndex = -1 });
                x--;
            }
        }
        
        return operations;
    }
    
    /// <summary>
    /// 转换编辑脚本为DiffLine列表
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
    /// 词级差异算法
    /// </summary>
    private List<DiffCharacterRange> ComputeWordLevelDiff(string oldText, string newText, DiffHighlightOptions options)
    {
        var oldWords = SplitIntoWords(oldText);
        var newWords = SplitIntoWords(newText);
        
        var wordEditScript = ComputeMyersWordDiff(oldWords, newWords);
        return ConvertWordEditScriptToCharacterRanges(wordEditScript, oldText, newText);
    }
    
    /// <summary>
    /// 字符级差异算法（改进版）
    /// </summary>
    private List<DiffCharacterRange> ComputeCharacterLevelDiff(string oldText, string newText, DiffHighlightOptions options)
    {
        if (oldText.Length > options.MaxCharacterDiffLength || newText.Length > options.MaxCharacterDiffLength)
        {
            // 对于过长的文本，使用简化算法
            return ComputeSimplifiedCharacterDiff(oldText, newText);
        }
        
        var oldChars = oldText.ToCharArray().Select(c => c.ToString()).ToArray();
        var newChars = newText.ToCharArray().Select(c => c.ToString()).ToArray();
        
        var charEditScript = ComputeMyersCharDiff(oldChars, newChars);
        return ConvertCharEditScriptToCharacterRanges(charEditScript, oldText, newText);
    }
    
    /// <summary>
    /// 将文本分割为单词
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
    /// Myers算法的词级版本
    /// </summary>
    private List<EditOperation> ComputeMyersWordDiff(string[] oldWords, string[] newWords)
    {
        // 使用与行级相同的Myers算法，但应用于单词
        return ComputeMyersGeneric(oldWords, newWords, (a, b) => a.Equals(b));
    }
    
    /// <summary>
    /// Myers算法的字符级版本
    /// </summary>
    private List<EditOperation> ComputeMyersCharDiff(string[] oldChars, string[] newChars)
    {
        return ComputeMyersGeneric(oldChars, newChars, (a, b) => a.Equals(b));
    }
    
    /// <summary>
    /// 通用的Myers算法实现
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
    /// 构建通用编辑脚本
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
    /// 转换词级编辑脚本为字符范围
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
    /// 转换字符级编辑脚本为字符范围
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
    /// 简化的字符级差异算法（用于长文本）
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
        
        // 简单的前缀/后缀匹配
        var prefixLength = 0;
        var suffixLength = 0;
        var minLength = Math.Min(oldText.Length, newText.Length);
        
        // 找到共同前缀
        while (prefixLength < minLength && oldText[prefixLength] == newText[prefixLength])
        {
            prefixLength++;
        }
        
        // 找到共同后缀
        while (suffixLength < minLength - prefixLength && 
               oldText[oldText.Length - 1 - suffixLength] == newText[newText.Length - 1 - suffixLength])
        {
            suffixLength++;
        }
        
        // 中间部分作为差异
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
    /// 规范化行内容
    /// </summary>
    private string[] NormalizeLines(string[] lines, DiffHighlightOptions options)
    {
        return lines.Select(line => NormalizeLine(line, options)).ToArray();
    }
    
    /// <summary>
    /// 规范化单行内容
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
    /// 为修改的行添加字符级差异
    /// </summary>
    private void AddCharacterDiffs(List<DiffLine> lines, DiffHighlightOptions options)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            
            // 寻找可能的修改行对
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
/// 编辑操作类型
/// </summary>
public enum EditType
{
    Equal,
    Delete,
    Insert
}

/// <summary>
/// 编辑操作
/// </summary>
public class EditOperation
{
    public EditType Type { get; set; }
    public int OldIndex { get; set; }
    public int NewIndex { get; set; }
}