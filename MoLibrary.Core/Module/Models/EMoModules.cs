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
    /// <summary>
    /// 堆栈跟踪UI模块
    /// </summary>
    UIStackTrace,
    RpcClient,
    DaprProviderRpcClient,
    DaprProviderClientConnector,
    JobScheduler,
    /// <summary>
    /// 作业调度 UI 模块
    /// </summary>
    JobSchedulerUI,
    /// <summary>
    /// 作业调度 EF Core 持久化模块
    /// </summary>
    JobSchedulerEfCore,
    LoggingUI,
    Clock,
    /// <summary>
    /// 事件总线 UI 监控模块
    /// </summary>
    EventBusUI,
    /// <summary>
    /// 异常池模块
    /// </summary>
    ExceptionPool,
    /// <summary>
    /// HostedService 可观测性模块
    /// </summary>
    HostedService
}