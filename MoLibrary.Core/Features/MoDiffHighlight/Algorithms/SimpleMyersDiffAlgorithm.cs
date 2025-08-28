using MoLibrary.Core.Features.MoDiffHighlight.Models;

namespace MoLibrary.Core.Features.MoDiffHighlight.Algorithms;

/// <summary>
/// Myers 差异算法实现
/// </summary>
public class SimpleMyersDiffAlgorithm : IDiffAlgorithm
{
    /// <summary>
    /// 计算两个文本的差异
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
            // 处理删除的行
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
            
            // 处理新增的行
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
            
            // 处理相同的行
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
        
        // 处理剩余的删除行
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
        
        // 处理剩余的新增行
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
        
        // 添加字符级差异（如果启用）
        if (options.Mode == EDiffHighlightMode.Character || options.Mode == EDiffHighlightMode.Mixed)
        {
            AddCharacterDiffs(result, options);
        }
        
        return result;
    }
    
    /// <summary>
    /// 计算字符级差异
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
        
        // 简化的字符级差异算法
        var oldChars = oldText.ToCharArray();
        var newChars = newText.ToCharArray();
        var charLcs = ComputeCharacterLCS(oldChars, newChars);
        
        int oldPos = 0;
        int newPos = 0;
        
        foreach (var (oldCharPos, newCharPos) in charLcs)
        {
            // 删除的字符
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
            
            // 新增的字符
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
    /// 规范化行内容（处理忽略空白字符、忽略大小写等选项）
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
    /// 计算最长公共子序列 (LCS)
    /// </summary>
    private List<(int, int)> ComputeLCS(string[] oldLines, string[] newLines)
    {
        int m = oldLines.Length;
        int n = newLines.Length;
        var dp = new int[m + 1, n + 1];
        
        // 动态规划计算 LCS 长度
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
        
        // 回溯构建 LCS 序列
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
    /// 计算字符级最长公共子序列
    /// </summary>
    private List<(int, int)> ComputeCharacterLCS(char[] oldChars, char[] newChars)
    {
        int m = oldChars.Length;
        int n = newChars.Length;
        
        if (m > 1000 || n > 1000) // 避免性能问题
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
    /// 为修改的行添加字符级差异
    /// </summary>
    private void AddCharacterDiffs(List<DiffLine> lines, DiffHighlightOptions options)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            
            // 寻找可能的修改行对（一个删除行紧跟一个新增行）
            if (line.Type == EDiffLineType.Deleted && 
                i + 1 < lines.Count && 
                lines[i + 1].Type == EDiffLineType.Added)
            {
                var deletedLine = line;
                var addedLine = lines[i + 1];
                
                // 如果行内容长度不超过限制，进行字符级对比
                if (deletedLine.OldContent.Length <= options.MaxCharacterDiffLength &&
                    addedLine.NewContent.Length <= options.MaxCharacterDiffLength)
                {
                    var charDiffs = ComputeCharacterDiff(deletedLine.OldContent, addedLine.NewContent, options);
                    
                    // 如果有字符级差异，标记为修改行
                    if (charDiffs.Any())
                    {
                        deletedLine.Type = EDiffLineType.Modified;
                        deletedLine.NewContent = addedLine.NewContent;
                        deletedLine.NewLineNumber = addedLine.NewLineNumber;
                        deletedLine.CharacterDiffs = charDiffs;
                        
                        // 移除重复的新增行
                        lines.RemoveAt(i + 1);
                    }
                }
            }
        }
    }
}