using MoLibrary.JobScheduler.Models;
using MudBlazor;

namespace MoLibrary.JobScheduler.UI.Services;

/// <summary>
/// 作业状态颜色服务
/// 提供统一的作业状态到 MudBlazor 颜色的映射
/// </summary>
public class JobStateColorService
{
    /// <summary>
    /// 获取作业状态对应的 MudBlazor 颜色
    /// </summary>
    /// <param name="state">作业状态</param>
    /// <returns>对应的 MudBlazor 颜色</returns>
    public Color GetStateColor(JobState state)
    {
        return state switch
        {
            JobState.Succeeded => Color.Success,
            JobState.Failed => Color.Error,
            JobState.Terminated => Color.Error,
            JobState.Cancelled => Color.Warning,
            JobState.Processing => Color.Primary,
            JobState.Enqueued => Color.Info,
            JobState.Scheduled => Color.Default,
            JobState.Skipped => Color.Surface,
            _ => Color.Default
        };
    }

    /// <summary>
    /// 获取作业状态对应的十六进制颜色值
    /// 用于图表等需要精确颜色控制的场景
    /// </summary>
    /// <param name="state">作业状态</param>
    /// <returns>十六进制颜色值</returns>
    public string GetStateColorHex(JobState state)
    {
        return state switch
        {
            JobState.Succeeded => "#4caf50",   // Success green
            JobState.Failed => "#f44336",       // Error red
            JobState.Terminated => "#ff5722",   // Deep orange
            JobState.Processing => "#2196f3",   // Primary blue
            JobState.Enqueued => "#00bcd4",     // Info cyan
            JobState.Scheduled => "#9c27b0",    // Purple
            JobState.Skipped => "#ff9800",      // Warning orange
            JobState.Cancelled => "#9e9e9e",    // Grey
            _ => "#757575"
        };
    }
}
