using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MoLibrary.Core.Features.MoDiffHighlight.Algorithms;
using MoLibrary.Core.Features.MoDiffHighlight.Models;
using MoLibrary.Core.Features.MoDiffHighlight.Renderers;

namespace MoLibrary.Core.Features.MoDiffHighlight;

using Microsoft.Extensions.Options;
using MoLibrary.Core.Modules;


/// <summary>
/// 默认差异对比高亮实现
/// </summary>
public class DefaultDiffHighlight(ILogger<DefaultDiffHighlight> logger, IOptions<ModuleDiffHighlightOption> options, IDiffAlgorithm algorithm) : IMoDiffHighlight
{
    private readonly ModuleDiffHighlightOption _option = options.Value;
    private IDiffHighlightRenderer _renderer = new HtmlDiffRenderer();
    
    /// <summary>
    /// 执行文本对比并生成高亮结果（异步版本）
    /// </summary>
    public async Task<DiffHighlightResult> HighlightAsync(string oldText, string newText, DiffHighlightOptions? options = null)
    {
        return await Task.Run(() => Highlight(oldText, newText, options));
    }
    
    /// <summary>
    /// 执行文本对比并生成高亮结果（同步版本）
    /// </summary>
    public DiffHighlightResult Highlight(string oldText, string newText, DiffHighlightOptions? options = null)
    {
        options ??= _option.GetDefaultOptions();
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            logger.LogDebug("开始文本差异对比，oldText长度: {OldLength}, newText长度: {NewLength}", 
                oldText?.Length ?? 0, newText?.Length ?? 0);
            
            // 处理空值
            oldText ??= string.Empty;
            newText ??= string.Empty;
            
            // 分割文本为行
            var oldLines = SplitTextIntoLines(oldText);
            var newLines = SplitTextIntoLines(newText);
            
            // 执行差异算法
            var diffLines = algorithm.ComputeDiff(oldLines, newLines, options);
            
            // 计算统计信息
            var statistics = ComputeStatistics(diffLines, oldLines.Length, newLines.Length);
            
            // 根据输出格式选择合适的渲染器
            var renderer = GetRendererForFormat(options.OutputFormat);
            
            // 渲染结果
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
    /// 设置自定义渲染器
    /// </summary>
    public void SetRenderer(IDiffHighlightRenderer renderer)
    {
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        logger.LogDebug("设置自定义渲染器: {RendererType}", renderer.GetType().Name);
    }
    
    /// <summary>
    /// 设置自定义算法
    /// </summary>
    public void SetAlgorithm(IDiffAlgorithm customAlgorithm)
    {
        algorithm = customAlgorithm ?? throw new ArgumentNullException(nameof(customAlgorithm));
        logger.LogDebug("设置自定义算法: {AlgorithmType}", customAlgorithm.GetType().Name);
    }
    
    /// <summary>
    /// 将文本分割为行数组
    /// </summary>
    private string[] SplitTextIntoLines(string text)
    {
        if (string.IsNullOrEmpty(text))
            return Array.Empty<string>();
        
        // 处理不同的换行符
        return text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
    }
    
    /// <summary>
    /// 计算差异统计信息
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
    /// 根据输出格式获取对应的渲染器
    /// </summary>
    private IDiffHighlightRenderer GetRendererForFormat(EDiffOutputFormat format)
    {
        // 如果自定义渲染器支持指定格式，使用自定义渲染器
        if (_renderer.SupportedFormat == format)
            return _renderer;
        
        // 否则使用默认渲染器
        return format switch
        {
            EDiffOutputFormat.Html => new HtmlDiffRenderer(),
            EDiffOutputFormat.Markdown => new MarkdownDiffRenderer(),
            EDiffOutputFormat.PlainText => new PlainTextDiffRenderer(),
            _ => new HtmlDiffRenderer()
        };
    }
}