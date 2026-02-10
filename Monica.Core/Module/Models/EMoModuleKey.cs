namespace Monica.Core.Module.Models;

/// <summary>
/// Monica 内置模块键枚举。
/// </summary>
public enum EMoModuleKey
{
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
    /// <summary>
    /// 服务调用模块
    /// </summary>
    ServiceInvocation,
    DataChannel,
    FrameworkMonitor,
    Locker,
    UnitOfWork,
    Mapper,
    SignalR,
    StateStore,
    /// <summary>
    /// Redis 状态存储模块
    /// </summary>
    RedisStateStore,
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
    /// <summary>
    /// Swagger UI 增强模块
    /// </summary>
    SwaggerUI,
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
    /// 可观测实例模块
    /// </summary>
    ObservableInstance,
    /// <summary>
    /// 可观测实例 UI 监控模块
    /// </summary>
    ObservableInstanceUI,
    /// <summary>
    /// HostedService 可观测性模块
    /// </summary>
    HostedService,
    /// <summary>
    /// 状态存储 UI 管理模块
    /// </summary>
    StateStoreUI,
    /// <summary>
    /// 性能分析 UI 模块
    /// </summary>
    ProfilingUI,
    /// <summary>
    /// 弹性策略模块 (Polly)
    /// </summary>
    Resilience,
    /// <summary>
    /// AI 模块 - 提供统一的 AI 服务抽象
    /// </summary>
    AI,
    /// <summary>
    /// AI UI 模块 - 提供 AI 聊天界面
    /// </summary>
    AIUI,
    /// <summary>
    /// CORS (Cross-Origin Resource Sharing) module
    /// </summary>
    Cors,
    /// <summary>
    /// Markdown document management module
    /// </summary>
    Markdown,
    /// <summary>
    /// Markdown document viewer UI module
    /// </summary>
    MarkdownUI,
    /// <summary>
    /// RAG (Retrieval-Augmented Generation) module
    /// </summary>
    RAG
}
