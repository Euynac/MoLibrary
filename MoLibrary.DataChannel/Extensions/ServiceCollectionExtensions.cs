using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MoLibrary.Core.Extensions;
using MoLibrary.Core.Module.ModuleController;
using MoLibrary.DataChannel.Dashboard.Controllers;
using MoLibrary.DataChannel.Interfaces;
using MoLibrary.DataChannel.Services;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.DataChannel.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册DataChannel
    /// </summary>
    /// <param name="services"></param>
    /// <param name="action"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static IServiceCollection AddDataChannel<TBuilderEntrance>(this IServiceCollection services, Action<DataChannelSetting>? action = null) where TBuilderEntrance : class, ISetupPipeline
    {
        services.ConfigActionWrapper(action, out var setting);

        DataChannelCentral.Setting = setting;

        services.AddSingleton<IDataChannelManager, DataChannelManager>();
        services.AddSingleton(typeof(ISetupPipeline), typeof(TBuilderEntrance));
        
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
            mvcOptions.Conventions.Add(new ModuleControllerModelConvention<DataChannelController>(setting));
        });


        return services;
    }

    /// <summary>
    /// 使用DataChannel中间件
    /// </summary>
    /// <param name="app"></param>
    public static void UseDataChannel(this IApplicationBuilder app)
    {
        //use ISetupPipeline
        if (app.ApplicationServices.GetService(typeof(ISetupPipeline)) is ISetupPipeline setup)
        {
            setup.Setup();
        }

        DataChannelCentral.StartBuild(app);
        
        // Channel initialization is now handled by the hosted service
    }
}