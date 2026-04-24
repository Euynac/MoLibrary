using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Monica.Core.HostedService.Abstractions;
using Monica.Core.HostedService.Models;
using Monica.Core.Modularity.Models;
using Monica.Core.ObservableInstance.Abstractions;
using Monica.Modules;
using Monica.ServiceDiscovery.Abstractions;
using Monica.ServiceDiscovery.Events;
using Polly;
using Polly.Registry;

namespace Monica.ServiceDiscovery.Services;

/// <summary>
/// StateStore-based service discovery client service.
/// Implements heartbeat, leader election using Polly resilience pipelines.
/// </summary>
public class ServiceDiscoveryClientHostedService(
    IRegistrationStateManager stateManager,
    ILeaderElectionService leaderService,
    IServiceDiscoveryClientInfo clientInfo,
    IOptions<ModuleServiceDiscoveryOption> option,
    IObservableInstanceRegistry observableManager,
    IOptions<ModuleHostedServiceOption> hostedServiceOptions,
    ResiliencePipelineProvider<string> pipelineProvider)
    : MoBackgroundService(observableManager, hostedServiceOptions), IServiceRegistrationCoordinator
{
    private readonly ModuleServiceDiscoveryOption _option = option.Value;
    private readonly TaskCompletionSource<bool> _registrationCompletionSource = new();
    private readonly Random _random = new();
    private readonly ResiliencePipeline _heartbeatPipeline = pipelineProvider.GetPipeline(ResiliencePipelineNames.ServiceDiscovery);

    private int _consecutiveFailures;

    public override string ServiceName => "ServiceDiscoveryClient";
    public override string? ServiceGroupId => nameof(BuiltInModuleKey.ServiceDiscovery);
    public override TimeSpan? HeartbeatInterval => null;

    public bool IsRegistered => RuntimeInfo.CurrentState == HostedServiceState.Running;

    public async Task<bool> WaitForRegistrationAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            await _registrationCompletionSource.Task.WaitAsync(cts.Token);
            return _registrationCompletionSource.Task.Result;
        }
        catch (OperationCanceledException)
        {
            RecordState($"Waiting for registration timed out ({timeout})", logLevel: LogLevel.Warning);
            return false;
        }
    }

    protected override async Task ExecuteBackgroundAsync(CancellationToken stoppingToken)
    {
        RecordState("Starting StateStore heartbeat loop", HostedServiceState.Starting);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExecuteHeartbeatWithResilienceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                RecordState("Heartbeat loop exception", HostedServiceState.Degraded, ex);
            }

            // Wait with jitter
            var jitter = _random.Next(
                -_option.Election.HeartbeatJitterMilliseconds,
                _option.Election.HeartbeatJitterMilliseconds);
            var delay = _option.Election.HeartbeatPeriod + TimeSpan.FromMilliseconds(jitter);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        // Graceful shutdown - trigger LeaderLost if we're the Leader
        if (leaderService.IsLeader)
        {
            RecordState("Service shutting down, triggering Leader lost", logLevel: LogLevel.Information);
            leaderService.TriggerLeaderLost(LeaderLostReason.GracefulShutdown);

            try
            {
                await stateManager.DeleteLeaderKeyAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                RecordState("Failed to delete Leader key", exception: ex, logLevel: LogLevel.Warning);
            }
        }
    }

    /// <summary>
    /// Execute heartbeat with Polly resilience pipeline.
    /// The pipeline handles retries automatically.
    /// </summary>
    private async Task ExecuteHeartbeatWithResilienceAsync(CancellationToken ct)
    {
        try
        {
            RegistrationResult? result = null;
            await _heartbeatPipeline.ExecuteAsync(async token =>
            {
                result = await stateManager.RegisterOrHeartbeatAsync(token);
                if (!result.Success)
                {
                    throw new HeartbeatFailedException(result.ErrorMessage ?? "Heartbeat failed");
                }
            }, ct);

            // Heartbeat succeeded (possibly after retries)
            // Update client info with actual registration/heartbeat times
            if (result?.HeartbeatTime.HasValue == true)
            {
                clientInfo.SetRegistrationTime(result.HeartbeatTime.Value);
                clientInfo.UpdateLastHeartbeatTime(result.HeartbeatTime.Value);
            }

            await HandleSuccessfulHeartbeatAsync(ct);
        }
        catch (HeartbeatFailedException ex)
        {
            // All retries exhausted - handle persistent failure
            await HandlePersistentHeartbeatFailureAsync(ex.Message, ct);
        }
    }

    /// <summary>
    /// Handle successful heartbeat
    /// </summary>
    private async Task HandleSuccessfulHeartbeatAsync(CancellationToken ct)
    {
        // Reset consecutive failures on success
        _consecutiveFailures = 0;

        // Ensure registration status is completed
        if (RuntimeInfo.CurrentState != HostedServiceState.Running)
        {
            _registrationCompletionSource.TrySetResult(true);
        }

        RecordState("Heartbeat successful", HostedServiceState.Running);

        // Check or maintain Leader status
        await CheckOrMaintainLeaderAsync(ct);
    }

    /// <summary>
    /// Handle persistent heartbeat failure (after all retries exhausted)
    /// </summary>
    private async Task HandlePersistentHeartbeatFailureAsync(string? errorMessage, CancellationToken ct)
    {
        _consecutiveFailures++;
        RecordState($"Heartbeat failed after retries: {errorMessage}, consecutive failures: {_consecutiveFailures}", HostedServiceState.Degraded);

        // If we're the Leader and heartbeat consistently fails, we should give up leadership
        if (leaderService.IsLeader)
        {
            RecordState("Leader heartbeat failed after retries, giving up leadership", logLevel: LogLevel.Warning);
            leaderService.TriggerLeaderLost(LeaderLostReason.NetworkIsolation);

            // Handle based on isolation mode
            if (_option.IsolationHandlingMode == EIsolationHandlingMode.FastShutdown)
            {
                RecordState("Isolation handling mode is FastShutdown, triggering service isolation event", logLevel: LogLevel.Warning);
            }
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Check or maintain Leader status
    /// </summary>
    private async Task CheckOrMaintainLeaderAsync(CancellationToken ct)
    {
        if (!leaderService.IsLeader)
        {
            // Not currently Leader, check if we should compete
            var leaderExists = await stateManager.LeaderExistsAsync(ct);
            if (!leaderExists)
            {
                await CompeteForLeaderAsync(ct);
            }
            else
            {
                RecordState("Leader exists, staying as Follower", logLevel: LogLevel.Debug);
            }
        }
        else
        {
            // Currently Leader, renew lease
            await RenewLeaderLeaseAsync(ct);
        }
    }

    /// <summary>
    /// Compete to become Leader
    /// </summary>
    private async Task CompeteForLeaderAsync(CancellationToken ct)
    {
        RecordState("Attempting to compete for Leader", logLevel: LogLevel.Debug);

        if (!await TryAcquireLeaderLeaseAsync(ct))
        {
            RecordState("Leader competition failed, another instance may have become Leader", logLevel: LogLevel.Debug);
        }
    }

    /// <summary>
    /// Attempts to acquire a fresh leader lease without emitting a LeaderLost/LeaderGained pair.
    /// Used both for follower competition and same-heartbeat inline recovery after lease expiration.
    /// </summary>
    private async Task<bool> TryAcquireLeaderLeaseAsync(CancellationToken ct)
    {
        var (success, state, eTag) = await stateManager.TryBecomeLeaderAsync(ct);

        if (!success || state == null || string.IsNullOrEmpty(eTag))
        {
            return false;
        }

        if (leaderService.IsLeader)
        {
            leaderService.UpdateETag(eTag);
            RecordState(
                "Leader lease expired but was reacquired within the same heartbeat, continuing as Leader",
                logLevel: LogLevel.Warning);
            return true;
        }

        leaderService.SetAsLeader(state.BecomeLeaderTime, eTag);
        RecordState("Successfully became Leader", HostedServiceState.Running);
        return true;
    }

    /// <summary>
    /// Renew Leader lease
    /// </summary>
    private async Task RenewLeaderLeaseAsync(CancellationToken ct)
    {
        var currentETag = leaderService.CurrentETag;
        if (string.IsNullOrEmpty(currentETag))
        {
            RecordState("Cannot renew Leader: ETag is empty", logLevel: LogLevel.Warning);
            leaderService.TriggerLeaderLost(LeaderLostReason.NetworkIsolation);
            return;
        }

        var (success, newETag, actualState, actualETag) = await stateManager.RenewLeaderLeaseAsync(currentETag, ct);

        if (success && !string.IsNullOrEmpty(newETag))
        {
            leaderService.UpdateETag(newETag);
            RecordState("Leader lease renewed successfully", logLevel: LogLevel.Debug);
            return;
        }

        var currentInstanceId = clientInfo.GetServiceStatus().InstanceId;

        if (actualState != null)
        {
            if (actualState.InstanceId != currentInstanceId)
            {
                // Another instance became Leader
                leaderService.TriggerLeaderLost(LeaderLostReason.LeaderKeyTakenByOther);
                RecordState($"Leader taken by another instance ({actualState.InstanceId})", HostedServiceState.Running);
            }
            else
            {
                // We're still Leader but ETag is stale - refresh
                RecordState($"ETag mismatch but still Leader, refreshing ETag: {currentETag} -> {actualETag}", logLevel: LogLevel.Information);
                leaderService.UpdateETag(actualETag!);
            }
        }
        else
        {
            RecordState("Leader key expired or deleted, attempting inline leader recovery", logLevel: LogLevel.Warning);

            if (await TryAcquireLeaderLeaseAsync(ct))
            {
                return;
            }

            RecordState("Leader key expired and inline recovery failed, leadership lost", logLevel: LogLevel.Warning);
            leaderService.TriggerLeaderLost(LeaderLostReason.LeaderKeyExpired);
        }
    }

    /// <summary>
    /// Internal exception for heartbeat failures
    /// </summary>
    private sealed class HeartbeatFailedException(string message) : Exception(message);
}
