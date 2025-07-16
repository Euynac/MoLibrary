using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Models;
using MoLibrary.Core.Module.ModuleController;
using MoLibrary.Core.Modules;
using MoLibrary.DataChannel.Dashboard.Controllers;
using MoLibrary.DataChannel.Interfaces;
using MoLibrary.DataChannel.Services;

namespace MoLibrary.DataChannel.Modules;

public class ModuleDataChannel(ModuleDataChannelOption option)
    : MoModuleWithDependencies<ModuleDataChannel, ModuleDataChannelOption, ModuleDataChannelGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.DataChannel;
    }

    public override void ConfigureServices(IServiceCollection services)
    {

        DataChannelCentral.Setting = Option;
        services.AddSingleton<IDataChannelManager, DataChannelManager>();
        // Add the hosted service for channel initialization
        services.AddHostedService<DataChannelInitializerService>();

        //services.AddControllers()
        //    .AddApplicationPart(typeof(DataChannelController).Assembly)
        //    .ConfigureApplicationPartManager(manager =>
        //    {
        //        // 动态控制Controller的启用
        //        manager.FeatureProviders.Add(new ModuleControllerFeatureProvider<DataChannelController>(setting));
        //    });
    }

    public override void ConfigureApplicationBuilder(IApplicationBuilder app)
    {
        //use ISetupPipeline
        if (app.ApplicationServices.GetService(typeof(ISetupPipeline)) is ISetupPipeline setup)
        {
            setup.Setup();
        }

        DataChannelCentral.StartBuild(app);

        // Channel initialization is now handled by the hosted service
    }

    public override void ConfigureEndpoints(IApplicationBuilder app)
    {

        DataChannelCentral.ConfigEndpoints(app);
    }

    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleControllersGuide>().Register().RegisterMoControllers<DataChannelController>(Option);
    }
}