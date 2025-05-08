using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Extensions;
using MoLibrary.Core.Module.ModuleController;
using MoLibrary.DataChannel.Dashboard.Controllers;
using MoLibrary.DataChannel.Interfaces;
using MoLibrary.DataChannel.Modules;
using MoLibrary.DataChannel.Services;

namespace MoLibrary.DataChannel.Extensions;

/// <summary>
/// 服务集合扩展类
/// 提供用于注册和配置数据通道服务的扩展方法
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册DataChannel服务到依赖注入容器
    /// </summary>
    /// <typeparam name="TBuilderEntrance">数据管道设置类型，必须实现ISetupPipeline接口</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="action">可选的配置委托，用于自定义DataChannel设置</param>
    /// <returns>配置后的服务集合</returns>
    /// <exception cref="InvalidOperationException">当配置无效时抛出</exception>
    public static IServiceCollection AddDataChannel<TBuilderEntrance>(this IServiceCollection services, Action<ModuleDataChannelOption>? action = null) where TBuilderEntrance : class, ISetupPipeline
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
    /// 在应用程序中启用并配置DataChannel中间件
    /// 初始化管道设置并启动管道构建
    /// </summary>
    /// <param name="app">应用程序构建器实例</param>
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