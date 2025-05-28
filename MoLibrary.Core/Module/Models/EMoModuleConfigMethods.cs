namespace MoLibrary.Core.Module.Models;

/// <summary>
/// 模块构建顺序
/// </summary>
public enum EMoModuleConfigMethods
{
    /// <summary>
    /// Module declares its dependencies on other modules.
    /// </summary>
    ClaimDependencies,
    
    /// <summary>
    /// Initializes the final configurations for the module.
    /// </summary>
    InitFinalConfigures,
    
    /// <summary>
    /// Configures the WebApplicationBuilder for the module.
    /// </summary>
    ConfigureBuilder,
    
    /// <summary>
    /// Configures the services for the module.
    /// </summary>
    ConfigureServices,
    
    /// <summary>
    /// Iterates through business types for the module.
    /// </summary>
    IterateBusinessTypes,
    
    /// <summary>
    /// Performs post-configuration of services after all services have been registered.
    /// </summary>
    PostConfigureServices,
    
    /// <summary>
    /// Configures the application builder for the module.
    /// </summary>
    ConfigureApplicationBuilder,
    
    /// <summary>
    /// Configures the endpoints for the module.
    /// </summary>
    ConfigureEndpoints,
  
}