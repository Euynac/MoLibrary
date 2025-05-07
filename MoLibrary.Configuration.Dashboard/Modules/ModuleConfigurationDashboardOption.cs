using MoLibrary.Configuration.Dashboard.Interfaces;
using MoLibrary.Configuration.Modules;

namespace MoLibrary.Configuration.Dashboard.Modules;

/// <summary>
/// Configuration options for the MoConfiguration Dashboard module
/// </summary>
public class ModuleConfigurationDashboardOption : ModuleConfigurationOption
{
    /// <summary>
    /// Gets or sets the type of the dashboard implementation
    /// </summary>
    public Type? DashboardImplementationType { get; set; }

    /// <summary>
    /// Indicates whether this is a dashboard client
    /// </summary>
    public bool IsClient { get; set; }

    /// <summary>
    /// Gets or sets the type of the register centre server connector
    /// </summary>
    public Type? RegisterCentreServerConnectorType { get; set; }

    /// <summary>
    /// Gets or sets the type of the register centre client
    /// </summary>
    public Type? RegisterCentreClientType { get; set; }

    /// <summary>
    /// Gets or sets the configuration store type
    /// </summary>
    public Type? ConfigurationStoreType { get; set; }

    /// <summary>
    /// Gets or sets the group name for OpenAPI documentation
    /// </summary>
    public string OpenApiGroupName { get; set; } = "配置中心";
} 