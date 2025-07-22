using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using MoLibrary.Configuration.Dashboard.Controllers;
using MoLibrary.Configuration.Dashboard.Interfaces;
using MoLibrary.Configuration.Dashboard.Model;
using MoLibrary.Configuration.Modules;
using MoLibrary.Core.Extensions;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Models;
using MoLibrary.Core.Modules;
using MoLibrary.RegisterCentre.Modules;
using MoLibrary.Repository.Transaction;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Configuration.Dashboard.Modules;

public class ModuleConfigurationDashboard(ModuleConfigurationDashboardOption option)
    : MoModuleWithDependencies<ModuleConfigurationDashboard, ModuleConfigurationDashboardOption, ModuleConfigurationDashboardGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.ConfigurationDashboard;
    }

    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleRegisterCentreGuide>().Register();
        DependsOnModule<ModuleConfigurationGuide>().Register();
        
        // 注册Controller依赖
        DependsOnModule<ModuleControllersGuide>().Register()
            .RegisterMoControllers<ConfigurationDashboardController>(Option)
            .RegisterMoControllers<ConfigurationClientController>(Option);
    }

}