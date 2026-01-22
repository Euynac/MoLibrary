using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.DataChannel.Dashboard.Pages;
using MoLibrary.DataChannel.Dashboard.Services;
using MoLibrary.UI.Modules;
using MudBlazor;

namespace MoLibrary.DataChannel.Modules;

/// <summary>
/// DataChannel UI模块，提供DataChannel的管理界面
/// </summary>
public class ModuleDataChannelUI(ModuleDataChannelUIOption option)
    : MoModuleWithDependencies<ModuleDataChannelUI, ModuleDataChannelUIOption, ModuleDataChannelUIGuide>(option)
{
    /// <summary>
    /// 获取当前模块枚举
    /// </summary>
    /// <returns>DataChannelUI模块枚举</returns>
    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.DataChannelUI;
    }

    /// <summary>
    /// 配置服务
    /// </summary>
    /// <param name="services">服务集合</param>
    public override void ConfigureServices(IServiceCollection services)
    {
        // 注册DataChannel服务
        services.AddScoped<DataChannelService>();
    }

    /// <summary>
    /// 声明依赖
    /// </summary>
    public override void ClaimDependencies()
    {
        if (!Option.DisableDataChannelPage)
        {
            // 依赖DataChannel核心模块
            DependsOnModule<ModuleDataChannelGuide>().Register();

            // 依赖UIStackTrace模块（用于异常堆栈跟踪可视化）
            DependsOnModule<ModuleUIStackTraceGuide>().Register();

            // 依赖UI核心模块，并注册UI页面
            DependsOnModule<ModuleUICoreGuide>().Register()
                .RegisterUIComponents(p => p.RegisterComponent<UIDataChannelPage>(
                    UIDataChannelPage.PAGE_URL,
                    "DataChannel管理",
                    Icons.Material.Filled.DataObject,
                    "监控",
                    addToNav: true,
                    navOrder: 30));
        }
    }
}

public static class ModuleDataChannelUIBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// 配置 DataChannelUI 模块
        /// </summary>
        public static ModuleDataChannelUIGuide AddDataChannelUI(Action<ModuleDataChannelUIOption>? action = null)
        {
            return new ModuleDataChannelUIGuide().Register(action);
        }
    }
}

/// <summary>
/// DataChannel UI模块指导
/// </summary>
public class ModuleDataChannelUIGuide : MoModuleGuide<ModuleDataChannelUI, ModuleDataChannelUIOption, ModuleDataChannelUIGuide>
{
    /// <summary>
    /// 获取请求的配置方法键
    /// </summary>
    /// <returns>配置方法键数组</returns>
    protected override string[] GetRequestedConfigMethodKeys()
    {
        return [];
    }
}

/// <summary>
/// DataChannel UI模块选项
/// </summary>
public class ModuleDataChannelUIOption : MoModuleOptionWithMinimalApi<ModuleDataChannelUI>
{
    /// <summary>
    /// 是否禁用DataChannel页面
    /// </summary>
    public bool DisableDataChannelPage { get; set; } = false;
} 