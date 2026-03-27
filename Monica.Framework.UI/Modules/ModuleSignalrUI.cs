using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
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
        /// Configuring the SignalrUI module
        /// </summary>
        public static ModuleSignalrUIGuide AddSignalRUI(Action<ModuleSignalrUIOption>? action = null)
        {
            return new ModuleSignalrUIGuide().Register(action);
        }
    }
}

[ModuleKey(EMoModuleKey.SignalrUI)]
public class ModuleSignalrUI(ModuleSignalrUIOption option)
    : MoModule<ModuleSignalrUI, ModuleSignalrUIOption, ModuleSignalrUIGuide>(option)
{

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
    /// Whether to disable the SignalR debugging page
    /// </summary>
    public bool DisableUISingalrPage { get; set; }

    /// <summary>
    /// The default AccessToken is used for SignalR debugging pages
    /// </summary>
    public string? DefaultAccessToken { get; set; }
}