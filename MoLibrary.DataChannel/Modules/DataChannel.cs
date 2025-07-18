using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.Core.Modules;
using MoLibrary.Dapr.Modules;
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
        DependsOnModule<ModuleControllersGuide>().Register().RegisterMoControllers<ModuleDataChannelController>(Option);
        DependsOnModule<ModuleDaprClientGuide>().Register();
    }
}

public static class ModuleDataChannelBuilderExtensions
{
    public static ModuleDataChannelGuide ConfigModuleDataChannel(this WebApplicationBuilder builder,
        Action<ModuleDataChannelOption>? action = null)
    {
        return new ModuleDataChannelGuide().Register(action);
    }
}


public class ModuleDataChannelGuide : MoModuleGuide<ModuleDataChannel, ModuleDataChannelOption, ModuleDataChannelGuide>
{

    protected override string[] GetRequestedConfigMethodKeys()
    {
        return [nameof(SetChannelBuilder)];
    }
    public ModuleDataChannelGuide SetChannelBuilder<TBuilderEntrance>()
    {
        ConfigureServices(context =>
        {
            context.Services.AddSingleton(typeof(ISetupPipeline), typeof(TBuilderEntrance));
        });
        return this;
    }
}

/// <summary>
/// 数据通道配置类
/// 用于配置数据通道的全局设置和选项
/// 实现了IMoModuleOptions接口，支持模块化配置
/// </summary>
public class ModuleDataChannelOption : MoModuleControllerOption<ModuleDataChannel>
{
    /// <summary>
    /// 最近异常保留数量
    /// </summary>
    public int RecentExceptionToKeep { get; set; } = 10;

    /// <summary>
    /// 初始化线程数
    /// </summary>
    public int InitThreadCount { get; set; } = 10;

}