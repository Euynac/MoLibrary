using Microsoft.Extensions.Logging;
using MoLibrary.Core.Features.MoDiffHighlight.Models;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Core.Features.MoDiffHighlight;

/// <summary>
/// 差异对比高亮服务
/// </summary>
public class DiffHighlightService(IMoDiffHighlight diffHighlight, ILogger<DiffHighlightService> logger)
{
    /// <summary>
    /// 执行文本差异对比并返回高亮结果
    /// </summary>
    /// <param name="oldText">原始文本</param>
    /// <param name="newText">新文本</param>
    /// <param name="options">配置选项</param>
    /// <returns>包含高亮结果的响应</returns>
    public async Task<Res<DiffHighlightResult>> HighlightAsync(string oldText, string newText, DiffHighlightOptions? options = null)
    {
        try
        {
            logger.LogDebug("执行文本差异对比，oldText长度: {OldLength}, newText长度: {NewLength}", 
                oldText?.Length ?? 0, newText?.Length ?? 0);
            
            var result = await diffHighlight.HighlightAsync(oldText, newText, options);
            
            logger.LogInformation("文本差异对比完成，处理时间: {ProcessingTime}ms, 变更数: {TotalChanges}",
                result.ProcessingTimeMs, result.Statistics.TotalChanges);
            
            return Res.Ok(result);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "文本差异对比参数错误: {Message}", ex.Message);
            return Res.Fail($"参数错误: {ex.Message}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "文本差异对比过程中发生错误");
            return Res.Fail($"文本差异对比失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 执行文本差异对比并返回高亮结果（同步版本）
    /// </summary>
    /// <param name="oldText">原始文本</param>
    /// <param name="newText">新文本</param>
    /// <param name="options">配置选项</param>
    /// <returns>包含高亮结果的响应</returns>
    public Res<DiffHighlightResult> Highlight(string oldText, string newText, DiffHighlightOptions? options = null)
    {
        try
        {
            logger.LogDebug("执行文本差异对比（同步），oldText长度: {OldLength}, newText长度: {NewLength}", 
                oldText?.Length ?? 0, newText?.Length ?? 0);
            
            var result = diffHighlight.Highlight(oldText, newText, options);
            
            logger.LogInformation("文本差异对比完成，处理时间: {ProcessingTime}ms, 变更数: {TotalChanges}",
                result.ProcessingTimeMs, result.Statistics.TotalChanges);
            
            return Res.Ok(result);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "文本差异对比参数错误: {Message}", ex.Message);
            return Res.Fail($"参数错误: {ex.Message}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "文本差异对比过程中发生错误");
            return Res.Fail($"文本差异对比失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 获取差异统计信息
    /// </summary>
    /// <param name="oldText">原始文本</param>
    /// <param name="newText">新文本</param>
    /// <param name="options">配置选项</param>
    /// <returns>包含统计信息的响应</returns>
    public async Task<Res<DiffStatistics>> GetStatisticsAsync(string oldText, string newText, DiffHighlightOptions? options = null)
    {
        try
        {
            logger.LogDebug("获取文本差异统计信息");
            
            var result = await diffHighlight.HighlightAsync(oldText, newText, options);
            
            logger.LogDebug("获取文本差异统计信息完成");
            return Res.Ok(result.Statistics);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取文本差异统计信息过程中发生错误");
            return Res.Fail($"获取统计信息失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 检查两个文本是否相同
    /// </summary>
    /// <param name="oldText">原始文本</param>
    /// <param name="newText">新文本</param>
    /// <param name="options">配置选项</param>
    /// <returns>是否相同</returns>
    public async Task<Res<bool>> IsIdenticalAsync(string oldText, string newText, DiffHighlightOptions? options = null)
    {
        try
        {
            logger.LogDebug("检查文本是否相同");
            
            var result = await diffHighlight.HighlightAsync(oldText, newText, options);
            var isIdentical = !result.HasChanges;
            
            logger.LogDebug("文本相同性检查完成，结果: {IsIdentical}", isIdentical);
            return Res.Ok(isIdentical);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "检查文本相同性过程中发生错误");
            return Res.Fail($"检查失败: {ex.Message}");
        }
    }
}