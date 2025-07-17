using Microsoft.Extensions.Logging;
using MoLibrary.Core.Features.MoTimekeeper;
using MoLibrary.FrameworkUI.UITimekeeper.Models;
using MoLibrary.Tool.General;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.FrameworkUI.UITimekeeper.Services;

/// <summary>
/// Timekeeper服务，实现核心业务逻辑
/// </summary>
/// <param name="logger">日志服务</param>
public class TimekeeperService(ILogger<TimekeeperService> logger)
{
    /// <summary>
    /// 获取Timekeeper统计状态
    /// </summary>
    /// <returns>统计信息列表</returns>
    public async Task<Res<List<TimekeeperStatisticsResponse>>> GetTimekeeperStatusAsync()
    {
        try
        {
            var res = MoTimekeeperBase.GetStatistics();
            var list = res.OrderByDescending(p => p.Value.Average).Select(p => new TimekeeperStatisticsResponse
            {
                Name = p.Key,
                Times = p.Value.Times,
                Average = $"{p.Value.Average:0.##}ms",
                CreateAt = p.Value.StartTime.ToString("yyyy-MM-dd HH:mm:ss"),
                TimesEveryMinutes = $"{p.Value.Times / Math.Max((DateTime.Now - p.Value.StartTime).TotalMinutes, 1):0.##}",
                AverageMemory = p.Value.AverageMemoryBytes?.FormatBytes(),
                LastMemory = p.Value.LastMemoryBytes?.FormatBytes(),
                LastDuration = $"{p.Value.LastDuration:0.##}ms",
                LastExecutedTime = p.Value.LastExecutedTime?.ToString("yyyy-MM-dd HH:mm:ss")
            }).ToList();

            logger.LogDebug("成功获取Timekeeper统计状态，共 {Count} 个计时器", list.Count);
            return Res.Ok(list);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取Timekeeper统计状态失败");
            return Res.Fail($"获取Timekeeper统计状态失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取当前正在运行的Timekeeper
    /// </summary>
    /// <returns>正在运行的Timekeeper信息列表</returns>
    public async Task<Res<List<RunningTimekeeperResponse>>> GetRunningTimekeepersAsync()
    {
        try
        {
            var runningTimekeepers = MoTimekeeperBase.GetRunningTimekeepers();
            var list = runningTimekeepers.OrderByDescending(p => p.Value.CurrentElapsedMs).Select(p => new RunningTimekeeperResponse
            {
                Name = p.Key,
                Content = p.Value.Content,
                StartTime = p.Value.StartTime.ToString("yyyy-MM-dd HH:mm:ss"),
                CurrentElapsed = $"{p.Value.CurrentElapsedMs}ms",
                RunningDuration = $"{(DateTime.Now - p.Value.StartTime).TotalSeconds:0.##}s"
            }).ToList();

            logger.LogDebug("成功获取正在运行的Timekeeper，共 {Count} 个正在运行", list.Count);
            return Res.Ok(list);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取正在运行的Timekeeper失败");
            return Res.Fail($"获取正在运行的Timekeeper失败: {ex.Message}");
        }
    }
} 