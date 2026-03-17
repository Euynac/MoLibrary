using Microsoft.Extensions.Logging;

namespace Monica.Core.Modularity.Interfaces;

public interface IMoModuleOption : IMoModuleOptionBase
{
    /// <summary>
    /// Logger used during module registration and initialization.
    /// </summary>
    ILogger Logger { get; set; }

    /// <summary>
    /// Disables the module instead of throwing when registration fails.
    /// When `true`, the system records the error and skips the module for the rest of the application lifetime.
    /// </summary>
    bool? DisableModuleIfHasException { get; set; }
    
    /// <summary>
    /// Gets whether the current module is disabled.
    /// </summary>
    bool IsDisabled { get; }
    
    /// <summary>
    /// Disables the current module manually.
    /// </summary>
    /// <param name="reason">The reason, which is written to the log.</param>
    void DisableModule(string reason = "Manual disable");

    /// <summary>
    /// Sets the minimum log level for the module logger.
    /// </summary>
    /// <param name="logLevel">The minimum log level to set for this module.</param>
    void SetModuleLogLevel(LogLevel logLevel);

    /// <summary>
    /// Disables the module log.
    /// </summary>
    void DisableModuleLog();
}


public interface IMoModuleOptionBase
{

}
public interface IMoModuleOptionBase<TModule> : IMoModuleOptionBase where TModule : IMoModule
{

}
public interface IMoModuleOption<TModule> : IMoModuleOption, IMoModuleOptionBase<TModule> where TModule : IMoModule
{

}

public interface IMoModuleExtraOption<TModule> : IMoModuleOptionBase<TModule> where TModule : IMoModule
{
}


