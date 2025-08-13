using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.Core.Modules;
using MoLibrary.FrameworkUI.Pages;
using MoLibrary.FrameworkUI.UIRegisterCentre.Services;
using MoLibrary.RegisterCentre.Modules;
using MoLibrary.UI.Modules;
using MudBlazor;

namespace MoLibrary.FrameworkUI.Modules;

public class ModuleRegisterCentreUI(ModuleRegisterCentreUIOption option)
    : MoModuleWithDependencies<ModuleRegisterCentreUI, ModuleRegisterCentreUIOption, ModuleRegisterCentreUIGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.RegisterCentreUI;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<RegisterCentreService>();
    }

    public override void ClaimDependencies()
    {
        if (!Option.DisableRegisterCentrePage)
        {
            DependsOnModule<ModuleRegisterCentreGuide>().Register();
            DependsOnModule<ModuleUICoreGuide>().Register()
                .RegisterUIComponents(p => p.RegisterComponent<UIRegisterCentrePage>(
                    UIRegisterCentrePage.REGISTERCENTRE_DEBUG_URL, 
                    "注册中心", 
                    Icons.Material.Filled.CloudQueue, 
                    "系统管理", 
                    addToNav: true, 
                    navOrder: 90));
        }
    }
}

public class ModuleRegisterCentreUIGuide : MoModuleGuide<ModuleRegisterCentreUI, ModuleRegisterCentreUIOption, ModuleRegisterCentreUIGuide>
{
}

public static class ModuleRegisterCentreUIBuilderExtensions
{
    public static ModuleRegisterCentreUIGuide ConfigModuleRegisterCentreUI(this WebApplicationBuilder builder, Action<ModuleRegisterCentreUIOption>? action = null)
    {
        return new ModuleRegisterCentreUIGuide().Register(action);
    }
}

public class ModuleRegisterCentreUIOption : MoModuleOption<ModuleRegisterCentreUI>
{ 
    public bool DisableRegisterCentrePage { get; set; }
    
    /// <summary>
    /// 需要在列表界面直接展示的元数据Key列表
    /// </summary>
    public List<string> DisplayMetadataKeys { get; set; } = new();
}