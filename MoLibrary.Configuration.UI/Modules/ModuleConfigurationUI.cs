using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Configuration.Modules;
using MoLibrary.Configuration.UI.Pages;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.Core.Modules;
using MoLibrary.UI.Modules;
using MudBlazor;

namespace MoLibrary.Configuration.UI.Modules;

/// <summary>
/// 配置管理UI模块扩展方法
/// </summary>
public static class ModuleConfigurationUIBuilderExtensions
{
    public static ModuleConfigurationUIGuide ConfigModuleConfigurationUI(this WebApplicationBuilder builder,
        Action<ModuleConfigurationUIOption>? action = null)
    {
        return new ModuleConfigurationUIGuide().Register(action);
    }
}

/// <summary>
/// 配置管理UI模块
/// </summary>
public class ModuleConfigurationUI(ModuleConfigurationUIOption option)
    : MoModuleWithDependencies<ModuleConfigurationUI, ModuleConfigurationUIOption, ModuleConfigurationUIGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.ConfigurationUI;
    }


    public override void ClaimDependencies()
    {
        if (!Option.DisableConfigurationPage)
        {
            // 依赖配置模块
            DependsOnModule<ModuleConfigurationGuide>().Register();

            // 依赖差异对比模块
            DependsOnModule<ModuleDiffHighlightGuide>().Register();

            // 依赖配置仪表板模块
            DependsOnModule<ModuleConfigurationDashboardGuide>().Register();

            // 依赖UI核心模块并注册UI组件
            DependsOnModule<ModuleUICoreGuide>().Register()
                .RegisterUIComponents(registry =>
                {
                    // 注册面板配置页面
                    registry.RegisterComponent<UIConfigurationDashboardPage>(
                        UIConfigurationDashboardPage.PAGE_URL,
                        "配置面板",
                        Icons.Material.Filled.Dashboard,
                        "系统管理",
                        addToNav: true,
                        navOrder: 101);
                });
        }
    }
}

/// <summary>
/// 配置管理UI模块配置指南
/// </summary>
public class ModuleConfigurationUIGuide : MoModuleGuide<ModuleConfigurationUI, ModuleConfigurationUIOption, ModuleConfigurationUIGuide>
{
    
}

/// <summary>
/// 配置管理UI模块选项
/// </summary>
public class ModuleConfigurationUIOption : MoModuleOption<ModuleConfigurationUI>
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
