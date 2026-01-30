using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core.Module;
using Monica.Core.Module.Interfaces;
using Monica.Core.Module.Models;
using Monica.StateStore.Modules;
using Monica.StateStore.UI.Pages;
using Monica.StateStore.UI.Services;
using Monica.UI.Modules;
using MudBlazor;

namespace Monica.StateStore.UI.Modules;

public static class ModuleStateStoreUIBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// 配置 StateStoreUI 模块
        /// </summary>
        public static ModuleStateStoreUIGuide AddStateStoreUI(Action<ModuleStateStoreUIOption>? action = null)
        {
            return new ModuleStateStoreUIGuide().Register(action);
        }
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
