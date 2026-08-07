using Dapr.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Monica.Core.HostedService.Abstractions;
using Monica.Core.HostedService.Models;
using Monica.Core.ObservableInstance.Abstractions;
using Monica.Dapr.Abstractions;
using Monica.Dapr.Models;
using Monica.Modules;

namespace Monica.Dapr.Services;

/// <summary>
/// Dapr sidecar health check coordinator.
/// Manages initial and periodic health checks for the Dapr sidecar and provides synchronization for dependent services.
/// Supports graceful application shutdown via fail-fast mode for Kubernetes pod recreation.
/// </summary>
public class DaprSidecarHealthCoordinator(
    DaprClient daprClient,
    IObservableInstanceRegistry observableManager,
    IServiceScopeFactory serviceScopeFactory,
    IHostApplicationLifetime applicationLifetime,
    IOptions<ModuleDaprClientOption> clientOptions,
    IOptions<ModuleHostedServiceOption> hostedServiceOptions,
    ILogger<DaprSidecarHealthCoordinator> logger)
    : MoBackgroundService(observableManager, hostedServiceOptions, serviceScopeFactory, logger), IDaprSidecarHealthCoordinator
{
    private readonly ModuleDaprClientOption _options = clientOptions.Value;

    // State management
    private readonly Lock _statusLock = new();
    private readonly TaskCompletionSource<bool> _initialHealthCompletionSource = new();
    private DateTime? _lastHealthyAt;
    private int _consecutiveFailures;

    /// <summary>
    /// Gets the current health status of the Dapr sidecar
    /// </summary>
    public DaprHealthStatus Status
    {
        get { lock (_statusLock) { return field; } }
        private set { lock (_statusLock) { field = value; } }
    } = DaprHealthStatus.NotStarted;

    /// <summary>
    /// Gets whether the Dapr sidecar is healthy and ready for use
    /// </summary>
    public bool IsHealthy => Status == DaprHealthStatus.Healthy;

    /// <summary>
    /// Gets the timestamp of the last successful health check
    /// </summary>
    public DateTime? LastHealthyAt => _lastHealthyAt;

    /// <summary>
    /// Gets the number of consecutive health check failures
    /// </summary>
    public int ConsecutiveFailures => _consecutiveFailures;

    /// <summary>
    /// Gets the name of this service for identification and monitoring
    /// </summary>
    public override string ServiceName => "DaprSidecarHealthCoordinator";
    public override string? ServiceGroupId => nameof(ModuleDaprClient);

    /// <summary>
    /// Waits for the Dapr sidecar to become healthy or timeout.
    /// </summary>
    public async Task<bool> WaitForHealthyAsync(TimeSpan timeout, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);

        try
        {
            await _initialHealthCompletionSource.Task.WaitAsync(cts.Token);
            return _initialHealthCompletionSource.Task.Result;
        }
        catch (OperationCanceledException)
        {
            Logger.LogWarning("Waiting for Dapr sidecar health timed out ({Timeout})", timeout);
            return false;
        }
    }

    /// <summary>
    /// Executes the background health check work: initial retries + continuous periodic monitoring.
    /// </summary>
    protected override async Task ExecuteBackgroundAsync(CancellationToken stoppingToken)
    {
        // Phase 1: Initial health check with retry and exponential backoff
        Status = DaprHealthStatus.Checking;
        RecordState("Checking Dapr sidecar health", HostedServiceState.Starting);

        var retryCount = _options.InitialRetryTimes;
        var currentDelay = _options.InitialRetryInterval;

        while (retryCount > 0 && !stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Use Dapr SDK's CheckOutboundHealthAsync method
                var isHealthy = await daprClient.CheckOutboundHealthAsync(stoppingToken);

                if (isHealthy)
                {
                    _lastHealthyAt = DateTime.UtcNow;
                    _consecutiveFailures = 0;
                    Status = DaprHealthStatus.Healthy;
                    RecordState("Dapr sidecar is healthy", HostedServiceState.Running);
                    _initialHealthCompletionSource.TrySetResult(true);

                    // Phase 2: Start periodic monitoring
                    await RunPeriodicHealthCheckAsync(stoppingToken);
                    return;
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex,
                    "Exception during Dapr health check (attempt {Attempt}/{Total})",
                    _options.InitialRetryTimes - retryCount + 1,
                    _options.InitialRetryTimes);
            }

            _consecutiveFailures++;
            Logger.LogWarning(
                "Dapr health check failed (attempt {Attempt}/{Total})",
                _options.InitialRetryTimes - retryCount + 1,
                _options.InitialRetryTimes);

            retryCount--;
            if (retryCount > 0)
            {
                await Task.Delay(currentDelay, stoppingToken);
                // Exponential backoff
                currentDelay = TimeSpan.FromMilliseconds(
                    currentDelay.TotalMilliseconds * _options.BackoffMultiplier);
                if (currentDelay > _options.MaxRetryInterval)
                    currentDelay = _options.MaxRetryInterval;
            }
        }

        if (_options.EnableFailFast)
        {
            // If fail-fast is enabled, trigger graceful application shutdown for K8s recreation.
            // During initial startup, we use InitialRetryTimes exhaustion as the trigger (not FailFastThreshold).
            Status = DaprHealthStatus.Failed;
            RecordState($"Failed to connect to Dapr sidecar after {_options.InitialRetryTimes} attempts", HostedServiceState.Faulted);
            Logger.LogCritical(
                "Fail-fast enabled: Failed to connect to Dapr sidecar after {Attempts} initial attempts. Initiating graceful application shutdown",
                _options.InitialRetryTimes);
            RecordState("Initiating graceful shutdown (fail-fast mode)", HostedServiceState.Faulted);
            applicationLifetime.StopApplication();
            return;
        }

        Status = DaprHealthStatus.Unhealthy;
        RecordState(
            $"Dapr sidecar is unavailable after {_options.InitialRetryTimes} initial attempts; continuing recovery checks",
            HostedServiceState.Degraded);
        Logger.LogWarning(
            "Dapr sidecar is unavailable after {Attempts} initial attempts. Continuing health checks every {Interval}",
            _options.InitialRetryTimes,
            _options.PeriodicCheckInterval);

        await RunPeriodicHealthCheckAsync(stoppingToken);
    }

    /// <summary>
    /// Runs periodic health check with state transitions and fail-fast support.
    /// </summary>
    private async Task RunPeriodicHealthCheckAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(_options.PeriodicCheckInterval, stoppingToken);

            try
            {
                // Use Dapr SDK's CheckOutboundHealthAsync method
                var isHealthy = await daprClient.CheckOutboundHealthAsync(stoppingToken);

                if (isHealthy)
                {
                    _lastHealthyAt = DateTime.UtcNow;
                    _consecutiveFailures = 0;
                    _initialHealthCompletionSource.TrySetResult(true);

                    if (Status != DaprHealthStatus.Healthy)
                    {
                        Status = DaprHealthStatus.Healthy;
                        RecordState("Dapr sidecar recovered to healthy state", HostedServiceState.Running);
                    }
                }
                else
                {
                    _consecutiveFailures++;
                    Logger.LogWarning(
                        "Dapr periodic health check failed (consecutive failures: {Count})",
                        _consecutiveFailures);

                    // Check fail-fast threshold first (most severe)
                    // During runtime periodic checks, we use FailFastThreshold (not InitialRetryTimes)
                    if (_options.EnableFailFast && _consecutiveFailures >= _options.FailFastThreshold)
                    {
                        Status = DaprHealthStatus.Failed;
                        RecordState($"Dapr sidecar failed: {_consecutiveFailures} consecutive failures reached fail-fast threshold ({_options.FailFastThreshold})", HostedServiceState.Faulted);
                        Logger.LogCritical(
                            "Fail-fast enabled: {Failures} consecutive runtime failures reached threshold ({Threshold}). Initiating graceful application shutdown",
                            _consecutiveFailures, _options.FailFastThreshold);
                        applicationLifetime.StopApplication();
                        return; // Exit periodic check - app is shutting down
                    }
                    // Otherwise check degraded/unhealthy thresholds
                    else if (_consecutiveFailures >= _options.UnhealthyThreshold)
                    {
                        Status = DaprHealthStatus.Unhealthy;
                        RecordState($"Dapr sidecar is unhealthy (consecutive failures: {_consecutiveFailures})", HostedServiceState.Degraded);
                    }
                    else if (_consecutiveFailures >= _options.DegradedThreshold)
                    {
                        Status = DaprHealthStatus.Degraded;
                        RecordState($"Dapr sidecar is degraded (consecutive failures: {_consecutiveFailures})", HostedServiceState.Degraded);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown scenario - exit cleanly
                break;
            }
            catch (Exception ex)
            {
                _consecutiveFailures++;
                Logger.LogWarning(ex,
                    "Exception during Dapr periodic health check (consecutive failures: {Count})",
                    _consecutiveFailures);

                // Apply same fail-fast and state transition logic as above
                if (_options.EnableFailFast && _consecutiveFailures >= _options.FailFastThreshold)
                {
                    Status = DaprHealthStatus.Failed;
                    RecordState($"Dapr sidecar failed: {_consecutiveFailures} consecutive failures reached fail-fast threshold ({_options.FailFastThreshold})", HostedServiceState.Faulted);
                    Logger.LogCritical(
                        "Fail-fast enabled: {Failures} consecutive runtime failures reached threshold ({Threshold}). Initiating graceful application shutdown",
                        _consecutiveFailures, _options.FailFastThreshold);
                    applicationLifetime.StopApplication();
                    return;
                }
                else if (_consecutiveFailures >= _options.UnhealthyThreshold)
                {
                    Status = DaprHealthStatus.Unhealthy;
                    RecordState($"Dapr sidecar is unhealthy (consecutive failures: {_consecutiveFailures})", HostedServiceState.Degraded);
                }
                else if (_consecutiveFailures >= _options.DegradedThreshold)
                {
                    Status = DaprHealthStatus.Degraded;
                    RecordState($"Dapr sidecar is degraded (consecutive failures: {_consecutiveFailures})", HostedServiceState.Degraded);
                }
            }
        }
    }
}
