using Microsoft.Extensions.Logging;

namespace Monica.Core.Modularity.Abstractions;

public interface IModuleOptions : IModuleOptionsBase
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


public interface IModuleOptionsBase
{

}
public interface IModuleOptionsBase<TModule> : IModuleOptionsBase where TModule : IModule
{

}
public interface IModuleOptions<TModule> : IModuleOptions, IModuleOptionsBase<TModule> where TModule : IModule
{

}

public interface IModuleExtraOptions<TModule> : IModuleOptionsBase<TModule> where TModule : IModule
{
}

/// <summary>
/// Internal host-context binding contract implemented by Monica module options.
/// </summary>
internal interface IModuleOptionsContext
{
    /// <summary>
    /// Binds the option instance to the host that owns it.
    /// </summary>
    /// <param name="application">The owning Monica application.</param>
    void Bind(MonicaApplication application);
}

