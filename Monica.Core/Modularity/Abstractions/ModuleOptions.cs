using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Monica.Core.Logging;

namespace Monica.Core.Modularity.Abstractions;

public class ModuleOptions<TModule> : IModuleOptions<TModule> where TModule : IModule
{
    /// <summary>
    /// Logger used during module registration and initialization.
    /// </summary>
    public ILogger Logger { get; set; } = LogManager.For<TModule>(Mo.Options.DefaultModuleLogLevel);

    /// <summary>
    /// Disables the module instead of throwing when registration fails.
    /// When `true`, the system records the error and skips the module for the rest of the application lifetime.
    /// </summary>
    public bool? DisableModuleIfHasException { get; set; } 

    /// <summary>
    /// Gets whether the current module is disabled.
    /// </summary>
    public bool IsDisabled { get; private set; }

    /// <summary>
    /// Disables the current module manually.
    /// </summary>
    /// <param name="reason">The reason, which is written to the log.</param>
    public void DisableModule(string reason = "Manual disable")
    {
        if (!IsDisabled)
        {
            IsDisabled = true;
            Logger.LogWarning("Module {ModuleType} has been manually disabled. Reason: {Reason}", 
                typeof(TModule).Name, reason);
        }
    }

    /// <summary>
    /// Sets the minimum log level for the module logger.
    /// </summary>
    /// <param name="logLevel">The minimum log level to set for this module.</param>
    public void SetModuleLogLevel(LogLevel logLevel)
    {
        Logger = LogManager.For<TModule>(logLevel);
    }

    /// <summary>
    /// Disables the module log.
    /// </summary>
    public void DisableModuleLog()
    {
        Logger = NullLogger.Instance;
    }
}
