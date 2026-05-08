using Microsoft.Extensions.Logging;

namespace Monica.Core;

/// <summary>
/// Read-only view of shared Monica module-system defaults.
/// </summary>
public interface IMonicaModuleSystemOptions
{
    /// <summary>
    /// Gets the default log level used by module registration loggers.
    /// </summary>
    LogLevel DefaultLogLevel { get; }

    /// <summary>
    /// Gets whether module registration failures disable the module instead of aborting startup.
    /// </summary>
    bool DisableOnRegistrationError { get; }

    /// <summary>
    /// Gets whether module execution summary logs are emitted after module system initialization.
    /// </summary>
    bool EnableSummaryLog { get; }

    /// <summary>
    /// Gets the default API group name for modules that expose endpoints.
    /// </summary>
    string? DefaultApiGroupName { get; }

    /// <summary>
    /// Gets the default Minimal API disabled state for modules.
    /// </summary>
    bool? DefaultMinimalApiDisabled { get; }
}

/// <summary>
/// Mutable configuration model for Monica module-system defaults.
/// Configure it through <see cref="Mo.ConfigModuleSystem"/>.
/// </summary>
public sealed class MonicaModuleSystemOptions : IMonicaModuleSystemOptions
{
    /// <summary>
    /// Gets or sets the default log level used by module registration loggers.
    /// Individual modules can still override their own logger through module options.
    /// Defaults to <see cref="LogLevel.Information"/>.
    /// </summary>
    public LogLevel DefaultLogLevel { get; set; } = LogLevel.Information;

    /// <summary>
    /// Gets or sets whether module registration failures disable the module instead of aborting startup.
    /// When enabled, the module system records the failure, logs it, and skips the module for the rest of the
    /// application lifetime. Individual module options can override this value.
    /// Defaults to <see langword="false"/>.
    /// </summary>
    public bool DisableOnRegistrationError { get; set; }

    /// <summary>
    /// Gets or sets whether module execution summary logs are emitted after module system initialization.
    /// Defaults to <see langword="false"/>.
    /// </summary>
    public bool EnableSummaryLog { get; set; }

    /// <summary>
    /// Gets or sets the default API group name for modules that expose endpoints.
    /// When unset, modules use their own fallback group names.
    /// </summary>
    public string? DefaultApiGroupName { get; set; }

    /// <summary>
    /// Gets or sets the default Minimal API disabled state for modules.
    /// When this value is <see langword="null"/>, modules use their own defaults.
    /// </summary>
    public bool? DefaultMinimalApiDisabled { get; set; }
}
