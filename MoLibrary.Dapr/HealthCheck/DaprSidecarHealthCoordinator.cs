using Dapr.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoLibrary.Core.Features.HostedServices;
using MoLibrary.Core.Features.HostedServices.Models;
using MoLibrary.Core.Features.ObservableInstance;
using MoLibrary.Core.Modules;
using MoLibrary.Dapr.Interfaces;
using MoLibrary.Dapr.Modules;

namespace MoLibrary.Dapr.HealthCheck;

/// <summary>
/// Dapr sidecar health check coordinator.
/// Manages initial and periodic health checks for the Dapr sidecar and provides synchronization for dependent services.
/// Supports graceful application shutdown via fail-fast mode for Kubernetes pod recreation.
/// </summary>
public class DaprSidecarHealthCoordinator(
    DaprClient daprClient,
    IObservableInstanceManager observableManager,
    IHostApplicationLifetime applicationLifetime,
    IOptions<ModuleDaprClientOption> clientOptions,
    IOptions<ModuleHostedServiceOption> hostedServiceOptions,
    ILogger<DaprSidecarHealthCoordinator> logger)
    : MoBackgroundService(observableManager, hostedServiceOptions, logger), IDaprSidecarHealthCoordinator
{
    private readonly ModuleDaprClientOption _options = clientOptions.Value;

    // State management
    private DaprHealthStatus _status = DaprHealthStatus.NotStarted;
    private readonly object _statusLock = new();
    private readonly TaskCompletionSource<bool> _initialHealthCompletionSource = new();
    private DateTime? _lastHealthyAt;
    private int _consecutiveFailures;

    /// <summary>
    /// Gets the current health status of the Dapr sidecar
    /// </summary>
    public DaprHealthStatus Status
    {
        get { lock (_statusLock) { return _status; } }
        private set { lock (_statusLock) { _status = value; } }
    }

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
        RecordStateChange(HostedServiceState.Starting, "Checking Dapr sidecar health");
        Logger.LogInformation("Starting initial Dapr sidecar health check");

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
                    RecordStateChange(HostedServiceState.Running, "Dapr sidecar is healthy");
                    Logger.LogInformation("Dapr sidecar is healthy");
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

        // Failed to become healthy after all initial retries
        Status = DaprHealthStatus.Failed;
        RecordStateChange(HostedServiceState.Faulted, $"Failed to connect to Dapr sidecar after {_options.InitialRetryTimes} attempts");
        Logger.LogError("Failed to connect to Dapr sidecar after {Attempts} attempts", _options.InitialRetryTimes);
        _initialHealthCompletionSource.TrySetResult(false);

        // If fail-fast is enabled, trigger graceful application shutdown for K8s recreation
        // During initial startup, we use InitialRetryTimes exhaustion as the trigger (not FailFastThreshold)
        if (_options.EnableFailFast)
        {
            Logger.LogCritical(
                "Fail-fast enabled: Failed to connect to Dapr sidecar after {Attempts} initial attempts. Initiating graceful application shutdown",
                _options.InitialRetryTimes);
            RecordStateChange(HostedServiceState.Faulted, "Initiating graceful shutdown (fail-fast mode)");
            applicationLifetime.StopApplication();
        }
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

                    if (Status != DaprHealthStatus.Healthy)
                    {
                        Status = DaprHealthStatus.Healthy;
                        RecordStateChange(HostedServiceState.Running, "Dapr sidecar recovered to healthy state");
                        Logger.LogInformation("Dapr sidecar recovered to healthy state");
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
                        RecordStateChange(
                            HostedServiceState.Faulted,
                            $"Dapr sidecar failed: {_consecutiveFailures} consecutive failures reached fail-fast threshold ({_options.FailFastThreshold})");
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
                        RecordStateChange(HostedServiceState.Degraded, $"Dapr sidecar is unhealthy (consecutive failures: {_consecutiveFailures})");
                        Logger.LogError("Dapr sidecar is unhealthy (consecutive failures: {Count})", _consecutiveFailures);
                    }
                    else if (_consecutiveFailures >= _options.DegradedThreshold)
                    {
                        Status = DaprHealthStatus.Degraded;
                        RecordStateChange(HostedServiceState.Degraded, $"Dapr sidecar is degraded (consecutive failures: {_consecutiveFailures})");
                        Logger.LogWarning("Dapr sidecar is degraded (consecutive failures: {Count})", _consecutiveFailures);
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
                    RecordStateChange(
                        HostedServiceState.Faulted,
                        $"Dapr sidecar failed: {_consecutiveFailures} consecutive failures reached fail-fast threshold ({_options.FailFastThreshold})");
                    Logger.LogCritical(
                        "Fail-fast enabled: {Failures} consecutive runtime failures reached threshold ({Threshold}). Initiating graceful application shutdown",
                        _consecutiveFailures, _options.FailFastThreshold);
                    applicationLifetime.StopApplication();
                    return;
                }
                else if (_consecutiveFailures >= _options.UnhealthyThreshold)
                {
                    Status = DaprHealthStatus.Unhealthy;
                    RecordStateChange(HostedServiceState.Degraded, $"Dapr sidecar is unhealthy (consecutive failures: {_consecutiveFailures})");
                    Logger.LogError("Dapr sidecar is unhealthy (consecutive failures: {Count})", _consecutiveFailures);
                }
                else if (_consecutiveFailures >= _options.DegradedThreshold)
                {
                    Status = DaprHealthStatus.Degraded;
                    RecordStateChange(HostedServiceState.Degraded, $"Dapr sidecar is degraded (consecutive failures: {_consecutiveFailures})");
                    Logger.LogWarning("Dapr sidecar is degraded (consecutive failures: {Count})", _consecutiveFailures);
                }
            }
        }
    }
}
