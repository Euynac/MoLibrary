using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Diagnostics.Models;

namespace Monica.Core;

/// <summary>
/// Read-only view of shared Monica module-system defaults.
/// </summary>
public interface IMonicaModuleSystemOptions
{
    /// <summary>
    /// Gets the maximum number of scheduled module startup work items that may run concurrently.
    /// </summary>
    int MaxConcurrentStartupWorkItems { get; }

    /// <summary>
    /// Gets optional host-defined startup performance budgets. Unset values do not imply a health threshold.
    /// </summary>
    IModuleStartupPerformanceBudgets? StartupPerformanceBudgets { get; }

    /// <summary>
    /// Gets the default log level used by module registration loggers.
    /// </summary>
    LogLevel DefaultLogLevel { get; }

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
    /// Gets the disclosure mode used when module configuration is explicitly requested through diagnostics.
    /// </summary>
    ModuleOptionDiagnosticsExposureMode OptionDiagnosticsExposureMode { get; }

    /// <summary>
    /// Gets the host used when Monica appends an HTTP listener for <see cref="MonicaEndpointPort"/>.
    /// </summary>
    string? MonicaEndpointHost { get; }
}

/// <summary>
/// Mutable configuration model for Monica module-system defaults.
/// Configure it through <see cref="IMonicaBuilder.ConfigureModuleSystem"/>.
/// </summary>
public sealed class MonicaModuleSystemOptions : IMonicaModuleSystemOptions
{
    private const int MIN_CONCURRENT_STARTUP_WORK_ITEMS = 1;
    private const int MIN_PORT = 1;
    private const int MAX_PORT = 65535;

    /// <summary>
    /// Gets or sets the maximum number of scheduled module startup work items that may run concurrently.
    /// Defaults to the current processor count. Set this to <c>1</c> to serialize startup work while preserving its
    /// barrier contracts. While modules can still submit required work, non-blocking work uses at most one fewer lane
    /// than this limit so required work always has execution capacity. Consequently, a value of <c>1</c> defers
    /// non-blocking work until submissions close. This limit does not change the serial ordering of module callbacks.
    /// </summary>
    public int MaxConcurrentStartupWorkItems { get; set; } = Math.Max(
        MIN_CONCURRENT_STARTUP_WORK_ITEMS,
        Environment.ProcessorCount);

    /// <summary>
    /// Gets optional startup performance budgets. Every value is unset by default, so Monica reports factual
    /// measurements without inventing a performance score or warning threshold.
    /// </summary>
    public ModuleStartupPerformanceBudgets? StartupPerformanceBudgets { get; set; }

    IModuleStartupPerformanceBudgets? IMonicaModuleSystemOptions.StartupPerformanceBudgets =>
        StartupPerformanceBudgets;

    /// <summary>
    /// Gets or sets the default log level used by module registration loggers.
    /// Defaults to <see cref="LogLevel.Information"/>.
    /// </summary>
    public LogLevel DefaultLogLevel { get; set; } = LogLevel.Information;

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
    /// Gets or sets the disclosure mode used by module option diagnostics. Every public option property is represented
    /// by name and type. The default exposes bounded ordinary values while redacting credentials and other sensitive
    /// values. Set
    /// <see cref="ModuleOptionDiagnosticsExposureMode.RevealSensitive"/> only for dedicated debugging because the
    /// resulting local UI may contain passwords, tokens, connection strings, and other secrets. The reveal mode is
    /// accepted only when the host environment is Development. Computed and runtime-shaped values remain
    /// metadata-only in every mode, and portable exports never contain options.
    /// </summary>
    public ModuleOptionDiagnosticsExposureMode OptionDiagnosticsExposureMode { get; set; } =
        ModuleOptionDiagnosticsExposureMode.Redacted;

    /// <summary>
    /// Gets or sets the host used when Monica appends an HTTP listener for <see cref="MonicaEndpointPort"/>.
    /// Use values such as <c>localhost</c>, <c>*</c>, <c>+</c>, <c>0.0.0.0</c>, or a concrete IP address.
    /// Leave this unset to derive the host from the application's existing URL bindings, falling back to
    /// <c>localhost</c> when no binding can be discovered before startup.
    /// </summary>
    public string? MonicaEndpointHost { get; set; }

    internal void Validate()
    {
        if (MaxConcurrentStartupWorkItems < MIN_CONCURRENT_STARTUP_WORK_ITEMS)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxConcurrentStartupWorkItems),
                MaxConcurrentStartupWorkItems,
                $"At least {MIN_CONCURRENT_STARTUP_WORK_ITEMS} startup work item must be allowed to run.");
        }

        StartupPerformanceBudgets?.Validate();

        if (!Enum.IsDefined(OptionDiagnosticsExposureMode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(OptionDiagnosticsExposureMode),
                OptionDiagnosticsExposureMode,
                "Module option diagnostics exposure mode must be a defined value.");
        }

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

    /// <summary>Rejects secret disclosure before host services are registered outside Development.</summary>
    internal void ValidateEnvironment(IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        if (OptionDiagnosticsExposureMode == ModuleOptionDiagnosticsExposureMode.RevealSensitive
            && !environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                $"{nameof(ModuleOptionDiagnosticsExposureMode.RevealSensitive)} module option diagnostics " +
                $"can be enabled only in the {Environments.Development} environment.");
        }
    }
}

/// <summary>Provides a read-only view of host-defined module startup performance budgets.</summary>
public interface IModuleStartupPerformanceBudgets
{
    /// <summary>Gets the end-to-end composition budget.</summary>
    TimeSpan? TotalComposition { get; }

    /// <summary>Gets the service-registration budget.</summary>
    TimeSpan? ServiceRegistration { get; }

    /// <summary>Gets the aggregate typed type-discovery-stage budget.</summary>
    TimeSpan? TypeDiscovery { get; }

    /// <summary>Gets the aggregate blocking startup-barrier budget.</summary>
    TimeSpan? AggregateBarrierWait { get; }

    /// <summary>Gets the longest individual serial module-callback budget.</summary>
    TimeSpan? LongestModuleCallback { get; }

    /// <summary>Gets the longest individual startup-work queue budget.</summary>
    TimeSpan? LongestStartupQueue { get; }
}

/// <summary>
/// Defines optional upper limits for module startup measurements. Every budget is disabled until explicitly set.
/// </summary>
public sealed class ModuleStartupPerformanceBudgets : IModuleStartupPerformanceBudgets
{
    /// <inheritdoc />
    public TimeSpan? TotalComposition { get; set; }

    /// <inheritdoc />
    public TimeSpan? ServiceRegistration { get; set; }

    /// <inheritdoc />
    public TimeSpan? TypeDiscovery { get; set; }

    /// <inheritdoc />
    public TimeSpan? AggregateBarrierWait { get; set; }

    /// <inheritdoc />
    public TimeSpan? LongestModuleCallback { get; set; }

    /// <inheritdoc />
    public TimeSpan? LongestStartupQueue { get; set; }

    internal void Validate()
    {
        ValidateBudget(TotalComposition, nameof(TotalComposition));
        ValidateBudget(ServiceRegistration, nameof(ServiceRegistration));
        ValidateBudget(TypeDiscovery, nameof(TypeDiscovery));
        ValidateBudget(AggregateBarrierWait, nameof(AggregateBarrierWait));
        ValidateBudget(LongestModuleCallback, nameof(LongestModuleCallback));
        ValidateBudget(LongestStartupQueue, nameof(LongestStartupQueue));
    }

    private static void ValidateBudget(TimeSpan? budget, string propertyName)
    {
        if (budget is { } value && value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                propertyName,
                value,
                "A configured module startup performance budget must be greater than zero.");
        }
    }
}
