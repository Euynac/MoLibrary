using MoLibrary.Core.Module.Interfaces;

namespace MoLibrary.JobScheduler.UI.Modules;

/// <summary>
/// JobScheduler UI 模块配置选项
/// </summary>
public class ModuleJobSchedulerUIOption : MoModuleOption<ModuleJobSchedulerUI>
{
    /// <summary>
    /// 禁用 JobScheduler UI 页面
    /// </summary>
    public bool DisableJobSchedulerPages { get; set; } = false;

    /// <summary>
    /// 健康指标时间窗口（默认 30 天）
    /// 配置统计近 x 时间健康度
    /// </summary>
    public TimeSpan HealthMetricsWindow { get; set; } = TimeSpan.FromDays(30);

    /// <summary>
    /// 健康指标中显示的最近失败实例数量（默认 5）
    /// 配置显示最近 x 个失败实例记录
    /// </summary>
    public int HealthMetricsFailedInstancesLimit { get; set; } = 5;

    /// <summary>
    /// 表格默认分页大小（默认 20）
    /// </summary>
    public int DefaultPageSize { get; set; } = 20;

    /// <summary>
    /// 自动刷新间隔（默认 5 秒）
    /// 设置为更高值（如 10 秒）以减少服务器负载
    /// </summary>
    public TimeSpan AutoRefreshInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// 默认启用自动刷新
    /// </summary>
    public bool EnableAutoRefreshByDefault { get; set; } = true;
}
