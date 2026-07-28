namespace Monica.Core.Modularity.Models;

/// <summary>
/// Built-in module keys used by Monica.
/// </summary>
public enum BuiltInModuleKey
{
    Authority,
    EventBus,
    BackgroundJob,
    Repository,
    Logging,
    DependencyInjection,
    AutoModel,
    WebApi,
    Configuration,
    /// <summary>
    /// Optional EventBus bridge module for distributed configuration reload notifications.
    /// </summary>
    ConfigurationEventBus,
    /// <summary>
    /// EF Core persistence module for configuration data.
    /// </summary>
    ConfigurationEfCore,
    /// <summary>
    /// Kafka provider and management console module for EventBus integrations.
    /// </summary>
    EventBusKafka,
    /// <summary>
    /// Kafka provider management console UI module for EventBus integrations.
    /// </summary>
    EventBusKafkaUI,
    /// <summary>
    /// Kafka management console EF Core persistence module.
    /// </summary>
    EventBusKafkaEfCore,
    Authentication,
    ConfigurationCenter,
    ServiceDiscovery,
    /// <summary>
    /// Service invocation module.
    /// </summary>
    ServiceInvocation,
    DataChannel,
    ProjectUnits,
    /// <summary>
    /// Source-level ProjectUnit analysis module.
    /// </summary>
    ProjectUnitsCodeAnalysis,
    Locker,
    UnitOfWork,
    ObjectMapping,
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
    Snowflake,
    ExecutionTiming,
    Excel,
    CancellationManager,
    TaskProgress,
    /// <summary>
    /// Job scheduling module.
    /// </summary>
    Scheduler,
    /// <summary>
    /// Runtime metrics collection module.
    /// </summary>
    RuntimeMetrics,
    /// <summary>
    /// OpenTelemetry SDK wiring and in-process metrics snapshot module.
    /// </summary>
    OpenTelemetry,
    /// <summary>
    /// Memory diagnostics module.
    /// </summary>
    MemoryDiagnostics,
    /// <summary>
    /// Type allocation tracking module.
    /// </summary>
    TypeAllocation,
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
    /// <summary>
    /// Module system diagnostics module.
    /// </summary>
    ModuleSystem,
    /// <summary>
    /// Module system UI module.
    /// </summary>
    ModuleSystemUI,
    FrameworkUI,
    /// <summary>
    /// Dependency-injection diagnostics UI module.
    /// </summary>
    DependencyInjectionUI,
    SignalRUI,
    SystemInfoUI,
    ExecutionTimingUI,
    /// <summary>
    /// Runtime metrics UI module.
    /// </summary>
    RuntimeMetricsUI,
    /// <summary>
    /// OpenTelemetry metrics dashboard UI module.
    /// </summary>
    OpenTelemetryUI,
    /// <summary>
    /// Memory analysis UI module.
    /// </summary>
    MemoryAnalysisUI,
    DataChannelUI,
    ConfigurationUI,
    MapperUI,
    ServiceDiscoveryUI,
    ProjectUnitsUI,
    /// <summary>
    /// Text diff highlight UI module.
    /// </summary>
    DiffHighlightUI,
    /// <summary>
    /// Stack trace UI module.
    /// </summary>
    UIStackTrace,
    RpcClient,
    DaprRpcClient,
    DaprServiceInvocation,
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
    /// Resilience policy module (Polly).
    /// </summary>
    Resilience,
    /// <summary>
    /// AI module that provides a unified AI service abstraction.
    /// </summary>
    AI,

    /// <summary>
    /// Optional stateless HTTP endpoints for the AI module.
    /// </summary>
    AIEndpoints,
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
    /// Repository diagnostics UI module.
    /// </summary>
    RepositoryUI,
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
    /// Local terminal command execution module.
    /// </summary>
    Terminal,
    /// <summary>
    /// Local terminal command execution UI module.
    /// </summary>
    TerminalUI,
    /// <summary>
    /// Practical utility module that provides connectivity probing and text transformation services.
    /// </summary>
    Utilities,
    /// <summary>
    /// Practical utility toolbox UI module.
    /// </summary>
    UtilitiesUI,
    /// <summary>
    /// AI skill discovery and agent skill provider module.
    /// </summary>
    AISkillSystem,
    /// <summary>
    /// MCP server discovery, hosting, and client catalog module.
    /// </summary>
    Mcp,
    /// <summary>
    /// Knowledge-base inventory and lookup module.
    /// </summary>
    KnowledgeBase,
    /// <summary>
    /// Knowledge-base UI module.
    /// </summary>
    KnowledgeBaseUI,
    /// <summary>
    /// RAG (Retrieval-Augmented Generation) module.
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
