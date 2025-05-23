namespace MoLibrary.Core.Module;

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
    DomainDrivenDesign
}


public enum EMoModuleOrder
{
    Normal = 0,
    PostConfig = 100,
    PreConfig = -100
}