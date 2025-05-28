using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Models;
using MoLibrary.Core.Module.ModuleController;
using MoLibrary.DataChannel.Dashboard.Controllers;
using MoLibrary.DataChannel.Interfaces;
using MoLibrary.DataChannel.Services;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.DataChannel.Modules;

public class ModuleDataChannel(ModuleDataChannelOption option)
    : MoModule<ModuleDataChannel, ModuleDataChannelOption>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.DataChannel;
    }

    public override Res ConfigureServices(IServiceCollection services)
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
        // 添加自定义Convention
        services.Configure<MvcOptions>(mvcOptions =>
        {
            mvcOptions.Conventions.Add(new ModuleControllerModelConvention<DataChannelController>(Option));
        });
        return Res.Ok();
    }

    public override Res ConfigureApplicationBuilder(IApplicationBuilder app)
    {
        //use ISetupPipeline
        if (app.ApplicationServices.GetService(typeof(ISetupPipeline)) is ISetupPipeline setup)
        {
            setup.Setup();
        }

        DataChannelCentral.StartBuild(app);

        // Channel initialization is now handled by the hosted service
        return base.ConfigureApplicationBuilder(app);
    }
}