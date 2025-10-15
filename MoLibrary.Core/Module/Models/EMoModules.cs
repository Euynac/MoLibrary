namespace MoLibrary.Core.Module.Models;

/// <summary>
/// MoLibraryModule列表
/// </summary>
public enum EMoModules
{
    /// <summary>
    /// 用户自身设置
    /// </summary>
    Developer,
    Authority,
    EventBus,
    BackgroundJob,
    Repository,
    Logging, 
    DependencyInjection,
    AutoModel,
    DomainDrivenDesign,
    Configuration,
    Authentication,
    ConfigurationDashboard,
    RegisterCentre,
    DataChannel,
    FrameworkMonitor,
    Locker,
    UnitOfWork,
    Mapper,
    SignalR,
    StateStore,
    Dapr,
    DaprClient,
    DaprStateStore,
    DaprEventBus,
    DaprLocker,
    GlobalExceptionHandler,
    AutoControllers,
    GlobalJson,
    Mediator,
    Swagger,
    Seeder,
    DynamicProxy,
    SnowflakeId,
    Timekeeper,
    Excel,
    CancellationManager,
    ProgressBar,
    /// <summary>
    /// 作业调度模块
    /// </summary>
    MoScheduler,
    Profiling,
    /// <summary>
    /// XML文档服务模块
    /// </summary>
    XmlDocumentation,
    /// <summary>
    /// 临时设置数据
    /// </summary>
    ScopedData,
    Controllers,
    /// <summary>
    /// 基本链路追踪模块
    /// </summary>
    ChainTracing,
    /// <summary>
    /// 框架链路追踪模块
    /// </summary>
    FrameworkChainTracing,
    /// <summary>
    /// 文本差异对比高亮模块
    /// </summary>
    DiffHighlight,
    /// <summary>
    /// UI 核心模块，用于界面基础构建
    /// </summary>
    UICore,
    FrameworkUI,
    SignalrUI,
    SystemInfoUI,
    TimekeeperUI,
    DataChannelUI,
    ConfigurationUI,
    MapperUI,
    RegisterCentreUI,
    FrameworkMonitorUI,
    /// <summary>
    /// 文本差异对比高亮UI模块
    /// </summary>
    DiffHighlightUI,
    RpcClient,
    RpcClientDaprProvider
}