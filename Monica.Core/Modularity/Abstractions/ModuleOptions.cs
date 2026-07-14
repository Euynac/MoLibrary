using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Monica.Core.Modularity.Abstractions;

public class ModuleOptions<TModule> : IModuleOptions<TModule>, IModuleOptionsContext where TModule : IModule
{
    private MonicaApplication? _application;
    private ILogger? _explicitLogger;
    private LogLevel? _minimumLogLevel;

    /// <summary>
    /// Logger used during module registration and initialization.
    /// </summary>
    public ILogger Logger
    {
        get
        {
            if (_explicitLogger is not null)
            {
                return _explicitLogger;
            }

            return _application is null
                ? NullLogger.Instance
                : _application.CreateLogger<TModule>(_minimumLogLevel ?? _application.ModuleSystem.DefaultLogLevel);
        }
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _explicitLogger = value;
        }
    }

    /// <summary>
    /// Gets the application identity defaults owned by the current host.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown before the option is bound to a Monica host.</exception>
    protected IMonicaApplicationOptions Application =>
        _application?.Application
        ?? throw new InvalidOperationException($"{GetType().Name} has not been bound to a Monica host.");

    /// <summary>
    /// Gets the module-system defaults owned by the current host.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown before the option is bound to a Monica host.</exception>
    protected IMonicaModuleSystemOptions ModuleSystem =>
        _application?.ModuleSystem
        ?? throw new InvalidOperationException($"{GetType().Name} has not been bound to a Monica host.");

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
        _minimumLogLevel = logLevel;
        _explicitLogger = null;
    }

    /// <summary>
    /// Disables the module log.
    /// </summary>
    public void DisableModuleLog()
    {
        _explicitLogger = NullLogger.Instance;
    }

    /// <summary>
    /// Binds host-owned defaults before developer configuration is applied.
    /// </summary>
    /// <param name="application">The owning Monica application.</param>
    void IModuleOptionsContext.Bind(MonicaApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);

        if (_application is not null && !ReferenceEquals(_application, application))
        {
            throw new InvalidOperationException($"{GetType().Name} is already bound to another Monica host.");
        }

        _application = application;
    }
}
