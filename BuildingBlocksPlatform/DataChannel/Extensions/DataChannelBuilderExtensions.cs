using BuildingBlocksPlatform.DataChannel.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocksPlatform.DataChannel.Extensions;

public static class DataChannelBuilderExtensions
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
        var setting = new DataChannelSetting();
        action?.Invoke(setting);
        DataChannelCentral.Setting = setting;

        services.AddSingleton<IDataChannelManager, DataChannelManager>();
        services.AddSingleton(typeof(ISetupPipeline), typeof(TBuilderEntrance));
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

        //异步初始化管道，避免某些管道需要等微服务构建完毕后才能初始化。TODO 是否有更好的方案
        Task.Factory.StartNew(async () =>
        {
            await Task.Delay(1000);
            InitAllChannel();
        });
    }

    internal static void InitAllChannel()
    {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        DataChannelCentral.Channels.Do(p=> p.Value.Pipe.InitAsync());
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
    }

}