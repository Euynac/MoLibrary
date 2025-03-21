using BuildingBlocksPlatform.DataChannel.Interfaces;
using BuildingBlocksPlatform.DataChannel.Pipeline;
using Microsoft.AspNetCore.Builder;

namespace BuildingBlocksPlatform.DataChannel;

/// <summary>
/// 消息通路中控
/// </summary>
public static class DataChannelCentral
{
    private static DataChannelSetting? _setting;
    internal static DataChannelSetting Setting
    {
        get => _setting ?? throw new InvalidOperationException(
            $"Setting is not initialized in {typeof(DataChannelCentral)}. Please register DataExchange first");
        set => _setting = value;
    }
    internal static ILogger Logger =>
        //如果没有初始化Logger，那么就使用ConsoleLogger
        Setting.Logger ??= LoggerFactory.Create(builder =>
        {
            builder.AddFilter("Microsoft", LogLevel.Warning)
                .AddFilter("System", LogLevel.Warning)
                .AddFilter("DataExchange", LogLevel.Debug)
                .AddConsole();
        }).CreateLogger("DataExchange");

    public static Dictionary<string, DataChannel> Channels { get; } = [];

    internal static List<DataPipelineBuilder> Builders { get; } = [];

    /// <summary>
    /// 注册交换管道为消息通路
    /// </summary>
    /// <param name="pipe"></param>
    public static void RegisterPipeline(DataPipeline pipe)
    {
        Channels.Add(pipe.Id, new DataChannel(pipe));
    }

    internal static void RegisterBuilder(DataPipelineBuilder builder)
    {
        Builders.Add(builder);
    }

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