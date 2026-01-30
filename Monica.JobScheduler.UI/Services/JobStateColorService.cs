using Monica.JobScheduler.Models;
using Monica.UI.Services;
using MudBlazor;
using MudBlazor.Utilities;

namespace Monica.JobScheduler.UI.Services;

/// <summary>
/// 作业状态颜色服务
/// 提供统一的作业状态到 MudBlazor 颜色的映射
/// </summary>
public class JobStateColorService(IMoThemeService themeService)
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
    /// 根据当前主题和明暗模式返回对应的颜色
    /// </summary>
    /// <param name="state">作业状态</param>
    /// <returns>十六进制颜色值</returns>
    public string GetStateColorHex(JobState state)
    {
        if(state == JobState.Skipped) return Colors.Gray.Lighten1;
        return themeService.GetColorHex(GetStateColor(state));
    }
}
