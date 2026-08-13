namespace Monica.Core.Modularity.Models;

/// <summary>
/// Module configuration phases.
/// </summary>
public enum ModulePhase
{
    None = 0,
    /// <summary>
    /// The module declares its option-free graph contract.
    /// </summary>
    Describe,
    
    /// <summary>
    /// Finalizes and validates the module's default options and named profiles.
    /// </summary>
    FinalizeOptions,

    /// <summary>
    /// Declares finalized option-dependent service contracts without mutating the host.
    /// </summary>
    DeclareContracts,
    
    /// <summary>
    /// Configures the IHostApplicationBuilder for the module.
    /// </summary>
    ConfigureBuilder,
    
    /// <summary>
    /// Configures the services for the module.
    /// </summary>
    ConfigureServices,
    
    /// <summary>
    /// Declares structural business-type queries for later centralized compilation and commit.
    /// </summary>
    DeclareTypeDiscovery,
    
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

    Disabled = 100
}
