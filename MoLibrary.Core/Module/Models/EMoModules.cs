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
    Profiling,
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
    FrameworkChainTracing
}