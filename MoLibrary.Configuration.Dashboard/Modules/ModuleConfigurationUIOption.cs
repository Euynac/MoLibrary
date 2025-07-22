using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.ModuleController;

namespace MoLibrary.Configuration.Dashboard.Modules;

/// <summary>
/// 配置管理UI模块选项
/// </summary>
public class ModuleConfigurationUIOption : MoModuleControllerOption<ModuleConfigurationUI>
{
    /// <summary>
    /// 是否禁用配置管理页面
    /// </summary>
    public bool DisableConfigurationPage { get; set; } = false;

    /// <summary>
    /// 页面标题
    /// </summary>
    public string PageTitle { get; set; } = "配置管理";

    /// <summary>
    /// 是否启用实时更新
    /// </summary>
    public bool EnableRealTimeUpdates { get; set; } = true;

    /// <summary>
    /// 默认页面大小
    /// </summary>
    public int DefaultPageSize { get; set; } = 20;

    /// <summary>
    /// 是否显示历史记录
    /// </summary>
    public bool ShowHistory { get; set; } = true;

    /// <summary>
    /// 历史记录保留天数
    /// </summary>
    public int HistoryRetentionDays { get; set; } = 180;

    /// <summary>
    /// 是否允许配置编辑
    /// </summary>
    public bool AllowEdit { get; set; } = true;

    /// <summary>
    /// 是否允许配置回滚
    /// </summary>
    public bool AllowRollback { get; set; } = true;
}