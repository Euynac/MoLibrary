namespace Monica.Core.Modularity.Models;

/// <summary>
/// Built-in module keys used by Monica.
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
    ServiceDiscovery,
    /// <summary>
    /// Service invocation module.
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
    /// Redis state store module.
    /// </summary>
    RedisStateStore,
    Dapr,
    DaprClient,
    DaprStateStore,
    DaprEventBus,
    DaprLocker,
    ExceptionHandling,
    AutoControllers,
    JsonSerialization,
    ResultEnvelope,
    Mediator,
    Swagger,
    /// <summary>
    /// Swagger UI enhancement module.
    /// </summary>
    SwaggerUI,
    Seeder,
    DynamicProxy,
    SnowflakeId,
    ExecutionTiming,
    Excel,
    CancellationManager,
    ProgressBar,
    /// <summary>
    /// Job scheduling module.
    /// </summary>
    MoScheduler,
    Profiling,
    /// <summary>
    /// XML documentation service module.
    /// </summary>
    XmlDocumentation,
    Controllers,
    /// <summary>
    /// Basic chain tracing module.
    /// </summary>
    ChainTracing,
    /// <summary>
    /// Text diff highlight module.
    /// </summary>
    DiffHighlight,
    /// <summary>
    /// Core UI module used as the foundation for UI features.
    /// </summary>
    UICore,
    FrameworkUI,
    SignalRUI,
    SystemInfoUI,
    ExecutionTimingUI,
    DataChannelUI,
    ConfigurationUI,
    MapperUI,
    ServiceDiscoveryUI,
    FrameworkMonitorUI,
    /// <summary>
    /// Text diff highlight UI module.
    /// </summary>
    DiffHighlightUI,
    /// <summary>
    /// Stack trace UI module.
    /// </summary>
    UIStackTrace,
    RpcClient,
    DaprProviderRpcClient,
    DaprProviderClientConnector,
    JobScheduler,
    /// <summary>
    /// Job scheduling UI module.
    /// </summary>
    JobSchedulerUI,
    /// <summary>
    /// Job scheduling EF Core persistence module.
    /// </summary>
    JobSchedulerEfCore,
    LoggingUI,
    Clock,
    /// <summary>
    /// Event bus UI monitoring module.
    /// </summary>
    EventBusUI,
    /// <summary>
    /// Observable instance module.
    /// </summary>
    ObservableInstance,
    /// <summary>
    /// Observable instance UI monitoring module.
    /// </summary>
    ObservableInstanceUI,
    /// <summary>
    /// HostedService observability module.
    /// </summary>
    HostedService,
    /// <summary>
    /// State store UI management module.
    /// </summary>
    StateStoreUI,
    /// <summary>
    /// Profiling UI module.
    /// </summary>
    ProfilingUI,
    /// <summary>
    /// Resilience policy module (Polly).
    /// </summary>
    Resilience,
    /// <summary>
    /// AI module that provides a unified AI service abstraction.
    /// </summary>
    AI,
    /// <summary>
    /// AI UI module that provides the AI chat experience.
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
    /// Git repository synchronization module.
    /// </summary>
    Git,
    /// <summary>
    /// Git repository dashboard UI module.
    /// </summary>
    GitUI,
    /// <summary>
    /// K8S operations module.
    /// </summary>
    K8S,
    /// <summary>
    /// K8S operations UI module.
    /// </summary>
    K8SUI,
    /// <summary>
    /// Emergency file operations module.
    /// </summary>
    FileOps,
    /// <summary>
    /// Emergency file operations UI module.
    /// </summary>
    FileOpsUI,
    /// <summary>
    /// RAG (Retrieval-Augmented Generation) module
    /// </summary>
    RAG,
    /// <summary>
    /// RAG UI module - provides RAG debug and management interface
    /// </summary>
    RAGUI,
    /// <summary>
    /// Localization module - provides JSON-based localization with culture fallback
    /// </summary>
    Localization
}
