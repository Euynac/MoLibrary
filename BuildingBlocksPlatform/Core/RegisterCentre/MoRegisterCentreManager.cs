using BuildingBlocksPlatform.Configuration.Annotations;

namespace BuildingBlocksPlatform.Core.RegisterCentre;

public static class MoRegisterCentreManager
{
    private static MoRegisterCentreSetting? _setting;

    internal static ILogger Logger =>
        //如果没有初始化Logger，那么就使用ConsoleLogger
        Setting.Logger ??= LoggerFactory.Create(builder =>
        {
            builder.AddFilter("Microsoft", LogLevel.Warning)
                .AddFilter("System", LogLevel.Warning)
                .AddFilter("MoRegisterCentre", LogLevel.Debug)
                .AddConsole();
        }).CreateLogger("MoRegisterCentre");


    internal static MoRegisterCentreSetting Setting
    {
        get => _setting ?? throw new InvalidOperationException(
            $"Setting is not initialized in {typeof(MoRegisterCentreManager)}. Please register MoRegisterCentre first");
        set => _setting = value;
    }

    /// <summary>
    /// 是否已经确认过是Client或Server
    /// </summary>
    internal static bool HasSetServerOrClient => _setting != null;
}

public class MoRegisterCentreSetting
{
    /// <summary>
    /// 日志记录器，不配置默认使用ConsoleLogger
    /// </summary>
    public ILogger? Logger { get; set; }
    /// <summary>
    /// 设定当前微服务是注册中心
    /// </summary>
    internal bool ThisIsCentreServer { get; set; } = false;
    /// <summary>
    /// 设定当前微服务是注册中心客户端
    /// </summary>
    internal bool ThisIsCentreClient { get; set; } = false;

    /// <summary>
    /// TODO 最大并发执行数量
    /// </summary>
    public int MaxParallelInvokerCount { get; set; }

    /// <summary>
    /// 客户端心跳频率
    /// </summary>
    public int HeartbeatDuration { get; set; } = 10;

    /// <summary>
    /// 客户端注册中心重试次数
    /// </summary>
    public int ClientRetryTimes { get; set; } = 3;
    /// <summary>
    /// 客户端重试频率
    /// </summary>
    public int RetryDuration { get; set; } = 5;

}