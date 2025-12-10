using MoLibrary.Core.Module.Interfaces;

namespace MoLibrary.Core.Modules;

/// <summary>
/// Configuration options for the HostedService observability module
/// </summary>
public class ModuleHostedServiceOption : MoModuleOption<ModuleHostedService>
{
    /// <summary>
    /// Gets or sets the default maximum history size for all services
    /// </summary>
    public int DefaultMaxHistorySize { get; set; } = 100; 

    /// <summary>
    /// Gets or sets the default heartbeat interval for BackgroundServices
    /// </summary>
    public TimeSpan DefaultHeartbeatInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets a value indicating whether exception pool should be enabled by default for all services
    /// </summary>
    public bool EnableExceptionPoolByDefault { get; set; } = true;
}
