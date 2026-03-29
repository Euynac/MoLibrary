using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Monica.Core.Features.MoDiffHighlight.Algorithms;
using Monica.Core.Features.MoDiffHighlight.Models;
using Monica.Core.Features.MoDiffHighlight.Renderers;

namespace Monica.Core.Features.MoDiffHighlight;

using Microsoft.Extensions.Options;
using Modules;


/// <summary>
/// Default implementation for text diff highlighting.
/// </summary>
public class DefaultDiffHighlight(ILogger<DefaultDiffHighlight> logger, IOptions<ModuleDiffHighlightOption> options, IDiffAlgorithm algorithm) : IMoDiffHighlight
{
    private readonly ModuleDiffHighlightOption _option = options.Value;
    private IDiffHighlightRenderer _renderer = new HtmlDiffRenderer();
    
    /// <summary>
    /// Generates a highlighted diff result asynchronously.
    /// </summary>
    public async Task<DiffHighlightResult> HighlightAsync(string oldText, string newText, DiffHighlightOptions? options = null)
    {
        return await Task.Run(() => Highlight(oldText, newText, options));
    }
    
    /// <summary>
    /// Generates a highlighted diff result synchronously.
    /// </summary>
    public DiffHighlightResult Highlight(string oldText, string newText, DiffHighlightOptions? options = null)
    {
        options ??= _option.GetDefaultOptions();
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            logger.LogDebug("开始文本差异对比，oldText长度: {OldLength}, newText长度: {NewLength}",
                oldText?.Length ?? 0, newText?.Length ?? 0);
            
            // Normalize null inputs so the diff pipeline can assume non-null strings.
            oldText ??= string.Empty;
            newText ??= string.Empty;
            
            var oldLines = SplitTextIntoLines(oldText);
            var newLines = SplitTextIntoLines(newText);
            
            var diffLines = algorithm.ComputeDiff(oldLines, newLines, options);
            
            var statistics = ComputeStatistics(diffLines, oldLines.Length, newLines.Length);
            
            // Select the renderer that matches the requested output format.
            var renderer = GetRendererForFormat(options.OutputFormat);
            
            var style = options.Style ?? new DiffHighlightStyle();
            var highlightedContent = renderer.Render(diffLines, style);
            
            stopwatch.Stop();
            
            var result = new DiffHighlightResult
            {
                HighlightedContent = highlightedContent,
                Statistics = statistics,
                Lines = diffLines,
                Options = options,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds
            };
            
            logger.LogDebug("文本差异对比完成，处理时间: {ProcessingTime}ms, 变更行数: {TotalChanges}",
                result.ProcessingTimeMs, result.Statistics.TotalChanges);
            
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "文本差异对比过程中发生错误");
            throw;
        }
        finally
        {
            stopwatch.Stop();
        }
    }
    
    /// <summary>
    /// Sets a custom renderer.
    /// </summary>
    public void SetRenderer(IDiffHighlightRenderer renderer)
    {
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        logger.LogDebug("设置自定义渲染器: {RendererType}", renderer.GetType().Name);
    }
    
    /// <summary>
    /// Sets a custom diff algorithm.
    /// </summary>
    public void SetAlgorithm(IDiffAlgorithm customAlgorithm)
    {
        algorithm = customAlgorithm ?? throw new ArgumentNullException(nameof(customAlgorithm));
        logger.LogDebug("设置自定义算法: {AlgorithmType}", customAlgorithm.GetType().Name);
    }
    
    /// <summary>
    /// Splits text into logical lines.
    /// </summary>
    private string[] SplitTextIntoLines(string text)
    {
        if (string.IsNullOrEmpty(text))
            return Array.Empty<string>();
        
        // Support Windows, Unix, and legacy Mac newline sequences.
        return text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
    }
    
    /// <summary>
    /// Builds summary statistics for the diff result.
    /// </summary>
    private DiffStatistics ComputeStatistics(List<DiffLine> diffLines, int totalOldLines, int totalNewLines)
    {
        var statistics = new DiffStatistics
        {
            TotalOldLines = totalOldLines,
            TotalNewLines = totalNewLines
        };
        
        foreach (var line in diffLines)
        {
            switch (line.Type)
            {
                case EDiffLineType.Added:
                    statistics.AddedLines++;
                    break;
                case EDiffLineType.Deleted:
                    statistics.DeletedLines++;
                    break;
                case EDiffLineType.Modified:
                    statistics.ModifiedLines++;
                    break;
                case EDiffLineType.Unchanged:
                    statistics.UnchangedLines++;
                    break;
            }
        }
        
        statistics.TotalChanges = statistics.AddedLines + statistics.DeletedLines + statistics.ModifiedLines;
        
        return statistics;
    }
    
    /// <summary>
    /// Gets the renderer for the requested output format.
    /// </summary>
    private IDiffHighlightRenderer GetRendererForFormat(EDiffOutputFormat format)
    {
        // Reuse the injected renderer when it supports the requested format.
        if (_renderer.SupportedFormat == format)
            return _renderer;
        
        // Otherwise fall back to the built-in renderer for that format.
        return format switch
        {
            EDiffOutputFormat.Html => new HtmlDiffRenderer(),
            EDiffOutputFormat.Markdown => new MarkdownDiffRenderer(),
            EDiffOutputFormat.PlainText => new PlainTextDiffRenderer(),
            _ => new HtmlDiffRenderer()
        };
    }
}
