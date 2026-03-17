using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.StateStore.UI.Pages;
using Monica.StateStore.UI.Services;
using Monica.StateStore.UI.Services.Browser;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

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
    : MoModule<ModuleStateStoreUI, ModuleStateStoreUIOption, ModuleStateStoreUIGuide>(option)
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
                    registry.RegisterLocalizedComponent<UIStateStoreDashboardPage>(
                        UIStateStoreDashboardPage.PAGE_URL,
                        "Pages:StateStoreManage:Title",
                        Icons.Material.Filled.Storage,
                        "Categories:Debug",
                        addToNav: true,
                        navOrder: 20);
                });
        }
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<StateStoreUIService>();
        services.AddSingleton<IStateStoreBrowserApi, RedisStateStoreBrowserApi>();
        services.AddSingleton<IStateStoreBrowserApi, MemoryStateStoreBrowserApi>();
        services.AddSingleton<IStateStoreBrowserApi, DaprStateStoreBrowserApi>();
        services.AddSingleton<IStateStoreBrowserApi, FallbackStateStoreBrowserApi>();
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
