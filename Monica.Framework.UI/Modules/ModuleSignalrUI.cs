using Microsoft.Extensions.DependencyInjection;
using Monica.Core.Module;
using Monica.Core.Module.Interfaces;
using Monica.Core.Module.Models;
using Monica.Framework.UI.UISignalr.Services;
using Monica.Framework.UI.Pages;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;


public static class ModuleSignalrUIBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// 配置 SignalrUI 模块
        /// </summary>
        public static ModuleSignalrUIGuide AddSignalRUI(Action<ModuleSignalrUIOption>? action = null)
        {
            return new ModuleSignalrUIGuide().Register(action);
        }
    }
}

public class ModuleSignalrUI(ModuleSignalrUIOption option)
    : MoModuleWithDependencies<ModuleSignalrUI, ModuleSignalrUIOption, ModuleSignalrUIGuide>(option)
{
    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.SignalrUI;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<SignalRDebugService>();
    }

    public override void ClaimDependencies()
    {
        if (!Option.DisableUISingalrPage)
        {
            DependsOnModule<ModuleSignalRGuide>().Register();
            DependsOnModule<ModuleUICoreGuide>().Register().RegisterUIComponents(p => p.RegisterLocalizedComponent<UISignalRPage>(UISignalRPage.UI_SIGNALR_URL, "Pages:SignalRDebug:Title", Icons.Material.Filled.Settings, "Categories:Debug", addToNav: true, navOrder: 20));
        }
    }
}

public class ModuleSignalrUIGuide : MoModuleGuide<ModuleSignalrUI, ModuleSignalrUIOption, ModuleSignalrUIGuide>
{


}

public class ModuleSignalrUIOption : MoModuleOption<ModuleSignalrUI>
{ 
    /// <summary>
    /// 是否禁用SignalR调试页面
    /// </summary>
    public bool DisableUISingalrPage { get; set; }

    /// <summary>
    /// 默认AccessToken用于SignalR调试页面
    /// </summary>
    public string? DefaultAccessToken { get; set; }
}