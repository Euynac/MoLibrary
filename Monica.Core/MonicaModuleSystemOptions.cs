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
    /// Gets whether Minimal API endpoints are enabled for modules by default.
    /// </summary>
    bool EnableMinimalApiByDefault { get; }

    /// <summary>
    /// Gets the local port that Monica-owned web endpoints must use.
    /// </summary>
    int? MonicaEndpointPort { get; }

    /// <summary>
    /// Gets whether Monica should append an HTTP wildcard listener for <see cref="MonicaEndpointPort"/>.
    /// </summary>
    bool AutoAddMonicaHttpListener { get; }

    /// <summary>
    /// Gets the host used when Monica appends an HTTP listener for <see cref="MonicaEndpointPort"/>.
    /// </summary>
    string? MonicaEndpointHost { get; }
}

/// <summary>
/// Mutable configuration model for Monica module-system defaults.
/// Configure it through <see cref="Mo.ConfigModuleSystem"/>.
/// </summary>
public sealed class MonicaModuleSystemOptions : IMonicaModuleSystemOptions
{
    private const int MIN_PORT = 1;
    private const int MAX_PORT = 65535;

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
    /// Gets or sets whether Minimal API endpoints are enabled for modules by default.
    /// Individual module options can still override this value.
    /// Defaults to <see langword="false"/>.
    /// </summary>
    public bool EnableMinimalApiByDefault { get; set; }

    /// <summary>
    /// Gets or sets the local port that Monica-owned web endpoints must use.
    /// Leave this value unset when Monica endpoints should be available on every host listener.
    /// When configured, Monica applies endpoint host constraints and verifies the accepted connection's
    /// local port after routing so requests on other ports behave as not found.
    /// </summary>
    public int? MonicaEndpointPort { get; set; }

    /// <summary>
    /// Gets or sets whether Monica appends an HTTP listener for <see cref="MonicaEndpointPort"/>.
    /// Defaults to <see langword="true"/> so setting <see cref="MonicaEndpointPort"/> is enough for common
    /// development and single-process hosting cases. Disable this when Kestrel endpoints, HTTPS, reverse proxy
    /// bindings, or deployment configuration should own listener setup explicitly.
    /// </summary>
    public bool AutoAddMonicaHttpListener { get; set; } = true;

    /// <summary>
    /// Gets or sets the host used when Monica appends an HTTP listener for <see cref="MonicaEndpointPort"/>.
    /// Use values such as <c>localhost</c>, <c>*</c>, <c>+</c>, <c>0.0.0.0</c>, or a concrete IP address.
    /// Leave this unset to derive the host from the application's existing URL bindings, falling back to
    /// <c>localhost</c> when no binding can be discovered before startup.
    /// </summary>
    public string? MonicaEndpointHost { get; set; }

    internal void Validate()
    {
        if (MonicaEndpointPort is null)
        {
            return;
        }

        if (MonicaEndpointPort is < MIN_PORT or > MAX_PORT)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MonicaEndpointPort),
                MonicaEndpointPort,
                $"Monica endpoint port must be between {MIN_PORT} and {MAX_PORT}.");
        }

        if (!string.IsNullOrWhiteSpace(MonicaEndpointHost)
            && (MonicaEndpointHost.Contains("://", StringComparison.Ordinal)
                || MonicaEndpointHost.Contains('/', StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"{nameof(MonicaEndpointHost)} must be a host name or IP address without scheme, port, or path.");
        }
    }
}
