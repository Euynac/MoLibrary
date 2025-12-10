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
    /// Gets or sets whether to fail fast (throw exception) when StartAsync encounters an error.
    /// When true, exceptions during service startup will be re-thrown, causing the application to fail fast.
    /// When false, exceptions are logged but not re-thrown, allowing the application to continue.
    /// Default is false for production stability.
    /// </summary>
    public bool FailFastOnStartupError { get; set; } = false;
}
