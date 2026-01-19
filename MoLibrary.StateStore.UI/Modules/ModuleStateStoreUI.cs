using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.StateStore.Modules;
using MoLibrary.StateStore.UI.Pages;
using MoLibrary.StateStore.UI.Services;
using MoLibrary.UI.Modules;
using MudBlazor;

namespace MoLibrary.StateStore.UI.Modules;

/// <summary>
/// StateStore UI 模块扩展方法
/// </summary>
public static class ModuleStateStoreUIBuilderExtensions
{
    /// <summary>
    /// 配置 StateStore UI 模块
    /// </summary>
    public static ModuleStateStoreUIGuide ConfigModuleStateStoreUI(
        this WebApplicationBuilder builder,
        Action<ModuleStateStoreUIOption>? action = null)
    {
        return new ModuleStateStoreUIGuide().Register(action);
    }
}

/// <summary>
/// StateStore UI 模块 - 提供状态存储管理界面
/// </summary>
public class ModuleStateStoreUI(ModuleStateStoreUIOption option)
    : MoModuleWithDependencies<ModuleStateStoreUI, ModuleStateStoreUIOption, ModuleStateStoreUIGuide>(option)
{
    public override ModuleKey GetModuleKey() => EMoModuleKey.StateStoreUI;

    public override void ClaimDependencies()
    {
        if (!Option.DisableStateStorePage)
        {
            // 依赖 StateStore 模块
            DependsOnModule<ModuleStateStoreGuide>().Register();

            // 依赖 UI 核心模块并注册 UI 组件
            DependsOnModule<ModuleUICoreGuide>().Register()
                .RegisterUIComponents(registry =>
                {
                    registry.RegisterComponent<UIStateStoreDashboardPage>(
                        UIStateStoreDashboardPage.PAGE_URL,
                        "状态存储管理",
                        Icons.Material.Filled.Storage,
                        "调试",
                        addToNav: true,
                        navOrder: 20);
                });
        }
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<StateStoreUIService>();
    }
}

/// <summary>
/// StateStore UI 模块配置指南
/// </summary>
public class ModuleStateStoreUIGuide
    : MoModuleGuide<ModuleStateStoreUI, ModuleStateStoreUIOption, ModuleStateStoreUIGuide>
{
}

/// <summary>
/// StateStore UI 模块选项
/// </summary>
public class ModuleStateStoreUIOption : MoModuleOption<ModuleStateStoreUI>
{
    /// <summary>
    /// 禁用 StateStore 管理页面
    /// </summary>
    public bool DisableStateStorePage { get; set; } = false;

    /// <summary>
    /// 默认 Key 扫描模式
    /// </summary>
    public string DefaultScanPattern { get; set; } = "*";

    /// <summary>
    /// 每页最大 Key 数量
    /// </summary>
    public int MaxKeysPerPage { get; set; } = 50;

    /// <summary>
    /// 允许编辑 Key (设为 false 则只读模式)
    /// </summary>
    public bool AllowKeyEditing { get; set; } = true;

    /// <summary>
    /// 允许删除 Key
    /// </summary>
    public bool AllowKeyDeletion { get; set; } = true;

    /// <summary>
    /// 允许创建 Key
    /// </summary>
    public bool AllowKeyCreation { get; set; } = true;
}
