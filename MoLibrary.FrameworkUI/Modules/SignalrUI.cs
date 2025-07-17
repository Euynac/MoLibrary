using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.FrameworkUI.Pages;
using MoLibrary.FrameworkUI.UISignalr.Services;
using MoLibrary.SignalR.Modules;
using MoLibrary.UI.Modules;
using MudBlazor;

namespace MoLibrary.FrameworkUI.Modules;


public static class ModuleSignalrUIBuilderExtensions
{
    public static ModuleSignalrUIGuide ConfigModuleSignalrUI(this WebApplicationBuilder builder,
        Action<ModuleSignalrUIOption>? action = null)
    {
        return new ModuleSignalrUIGuide().Register(action);
    }
}

public class ModuleSignalrUI(ModuleSignalrUIOption option)
    : MoModuleWithDependencies<ModuleSignalrUI, ModuleSignalrUIOption, ModuleSignalrUIGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.SignalrUI;
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
            DependsOnModule<ModuleUICoreGuide>().Register().RegisterUIComponents(p => p.RegisterComponent<UISignalRPage>(UISignalRPage.UI_SIGNALR_URL, "SignalR调试", Icons.Material.Filled.Settings, "系统管理", addToNav: true, navOrder: 100));
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