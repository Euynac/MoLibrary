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
    DaprEventBus
}

public enum EMoModuleConfigMethods
{
    ConfigureBuilder,
    ConfigureServices,
    PostConfigureServices,
    ConfigureApplicationBuilder,
    ConfigureEndpoints
}


public enum EMoModuleOrder
{
    Normal = 0,
    PostConfig = 100,
    PreConfig = -100
}