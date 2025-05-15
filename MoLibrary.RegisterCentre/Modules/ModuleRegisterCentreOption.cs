using Microsoft.Extensions.Logging;
using MoLibrary.Core.Module.Interfaces;

namespace MoLibrary.RegisterCentre.Modules;

public class ModuleRegisterCentreOption : MoModuleControllerOption<ModuleRegisterCentre>
{
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