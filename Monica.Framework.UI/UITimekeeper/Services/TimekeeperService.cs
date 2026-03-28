using Microsoft.Extensions.Logging;
using Monica.Core.Features.MoTimekeeper;
using Monica.Framework.UI.UITimekeeper.Models;
using Monica.Tool.General;
using Monica.Core.Results;

namespace Monica.Framework.UI.UITimekeeper.Services;

/// <summary>
/// Timekeeper service to implement core business logic
/// </summary>
/// <param name="logger">Log service</param>
public class TimekeeperService(ILogger<TimekeeperService> logger)
{
    /// <summary>
    /// Get Timekeeper statistical status
    /// </summary>
    /// <returns>statistical information list</returns>
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
    /// Get the currently running Timekeeper
    /// </summary>
    /// <returns>Running Timekeeper information list</returns>
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