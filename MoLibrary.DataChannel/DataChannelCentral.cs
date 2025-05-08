using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using MoLibrary.DataChannel.Interfaces;
using MoLibrary.DataChannel.Modules;
using MoLibrary.DataChannel.Pipeline;

namespace MoLibrary.DataChannel;

/// <summary>
/// 消息通路中控，负责管理和协调所有数据通道
/// 提供统一的注册、访问以及配置管理功能
/// </summary>
public static class DataChannelCentral
{
    private static ModuleDataChannelOption? _setting;
    
    /// <summary>
    /// 获取或设置数据通道的全局配置设置
    /// </summary>
    /// <exception cref="InvalidOperationException">当未初始化设置时抛出</exception>
    internal static ModuleDataChannelOption Setting
    {
        get => _setting ?? throw new InvalidOperationException(
            $"Setting is not initialized in {typeof(DataChannelCentral)}. Please register DataExchange first");
        set => _setting = value;
    }
    
    /// <summary>
    /// 获取全局日志记录器，如果未配置则创建默认控制台日志记录器
    /// </summary>
    internal static ILogger Logger =>
        //如果没有初始化Logger，那么就使用ConsoleLogger
        Setting.Logger ??= LoggerFactory.Create(builder =>
        {
            builder.AddFilter("Microsoft", LogLevel.Warning)
                .AddFilter("System", LogLevel.Warning)
                .AddFilter("DataExchange", LogLevel.Debug)
                .AddConsole();
        }).CreateLogger("DataExchange");

    /// <summary>
    /// 所有已注册的数据通道的字典集合，键为通道ID
    /// </summary>
    public static Dictionary<string, DataChannel> Channels { get; } = [];

    /// <summary>
    /// 所有数据管道构建器的集合
    /// </summary>
    internal static List<DataPipelineBuilder> Builders { get; } = [];

    /// <summary>
    /// 注册数据管道为消息通路
    /// 将一个已配置的数据管道添加到中央管理器中
    /// </summary>
    /// <param name="pipe">要注册的数据管道</param>
    public static void RegisterPipeline(DataPipeline pipe)
    {
        Channels.Add(pipe.Id, new DataChannel(pipe));
    }

    /// <summary>
    /// 注册一个数据管道构建器
    /// </summary>
    /// <param name="builder">要注册的数据管道构建器</param>
    internal static void RegisterBuilder(DataPipelineBuilder builder)
    {
        Builders.Add(builder);
    }

    /// <summary>
    /// 使用已注册的构建器开始构建所有数据管道
    /// 并对支持动态配置的组件执行应用程序配置
    /// </summary>
    /// <param name="app">应用程序构建器实例</param>
    internal static void StartBuild(IApplicationBuilder app)
    {
        foreach (var builder in Builders)
        {
            var pipe = builder.Build(app.ApplicationServices);
            foreach (var component in pipe.GetComponents())
            {
                if (component is IDynamicConfigApplicationBuilder config)
                    config.DoConfigApplication(app);
            }
        }
    }
}