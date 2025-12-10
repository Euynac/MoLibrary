using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;

namespace MoLibrary.Core.Modules;

/// <summary>
/// Fluent configuration guide for the HostedService observability module
/// </summary>
public class ModuleHostedServiceGuide
    : MoModuleGuide<ModuleHostedService, ModuleHostedServiceOption, ModuleHostedServiceGuide>
{
    public override EMoModules GetTargetModuleEnum() => EMoModules.HostedService;

    /// <summary>
    /// Sets the default maximum history size for all hosted services
    /// </summary>
    /// <param name="size">The maximum number of state history entries to retain</param>
    /// <returns>The guide instance for chaining</returns>
    public ModuleHostedServiceGuide SetDefaultMaxHistorySize(int size)
    {
        ConfigureModuleOption(opt => opt.DefaultMaxHistorySize = size);
        return this;
    }

    /// <summary>
    /// Sets the default heartbeat interval for all BackgroundServices
    /// </summary>
    /// <param name="interval">The heartbeat interval</param>
    /// <returns>The guide instance for chaining</returns>
    public ModuleHostedServiceGuide SetDefaultHeartbeatInterval(TimeSpan interval)
    {
        ConfigureModuleOption(opt => opt.DefaultHeartbeatInterval = interval);
        return this;
    }

    /// <summary>
    /// Disables exception pool by default for all services
    /// (services can still individually enable it via override)
    /// </summary>
    /// <returns>The guide instance for chaining</returns>
    public ModuleHostedServiceGuide DisableExceptionPoolByDefault()
    {
        ConfigureModuleOption(opt => opt.EnableExceptionPoolByDefault = false);
        return this;
    }
}
