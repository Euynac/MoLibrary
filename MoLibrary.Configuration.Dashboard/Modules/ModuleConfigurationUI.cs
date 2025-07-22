using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Configuration.Dashboard.Pages;
using MoLibrary.Configuration.Dashboard.UIConfiguration.Services;
using MoLibrary.Configuration.Modules;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Models;
using MoLibrary.UI.Modules;
using MoLibrary.UI.UICore.Interfaces;
using MudBlazor;

namespace MoLibrary.Configuration.Dashboard.Modules;

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

    public override void ConfigureServices(IServiceCollection services)
    {
        // 注册配置服务
        services.AddScoped<ConfigurationService>();
    }

    public override void ClaimDependencies()
    {
        if (!Option.DisableConfigurationPage)
        {
            // 依赖配置模块
            DependsOnModule<ModuleConfigurationGuide>().Register();
            
            // 依赖配置仪表板模块
            DependsOnModule<ModuleConfigurationDashboardGuide>().Register();

            // 依赖UI核心模块并注册UI组件
            DependsOnModule<ModuleUICoreGuide>().Register()
                .RegisterUIComponents(registry => registry.RegisterComponent<UIConfigurationPage>(
                    UIConfigurationPage.CONFIGURATION_DEBUG_URL, 
                    "配置管理", 
                    Icons.Material.Filled.Settings, 
                    "系统管理", 
                    addToNav: true, 
                    navOrder: 100));
        }
    }
}