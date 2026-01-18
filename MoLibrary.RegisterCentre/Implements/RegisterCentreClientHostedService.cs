using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoLibrary.Core.Features.HostedServices;
using MoLibrary.Core.Features.HostedServices.Models;
using MoLibrary.Core.Features.ObservableInstance;
using MoLibrary.Core.Modules;
using MoLibrary.RegisterCentre.Events;
using MoLibrary.RegisterCentre.Interfaces;
using MoLibrary.RegisterCentre.Models;
using MoLibrary.RegisterCentre.Modules;
using MoLibrary.Resilience.Modules;
using Polly;
using Polly.Registry;

namespace MoLibrary.RegisterCentre.Implements;

/// <summary>
/// StateStore-based RegisterCentre client service.
/// Implements heartbeat, leader election using Polly resilience pipelines.
/// </summary>
public class RegisterCentreClientHostedService(
    IRegistrationStateManager stateManager,
    ILeaderElectionService leaderService,
    IRegisterCentreClientInfo clientInfo,
    ILogger<RegisterCentreClientHostedService> logger,
    IOptions<ModuleRegisterCentreOption> option,
    IObservableInstanceManager observableManager,
    IOptions<ModuleHostedServiceOption> hostedServiceOptions,
    ResiliencePipelineProvider<string> pipelineProvider)
    : MoBackgroundService(observableManager, hostedServiceOptions, logger), IServiceRegistrationCoordinator
{
    private readonly ModuleRegisterCentreOption _option = option.Value;
    private readonly TaskCompletionSource<bool> _registrationCompletionSource = new();
    private readonly object _statusLock = new();
    private readonly Random _random = new();
    private readonly ResiliencePipeline _heartbeatPipeline = pipelineProvider.GetPipeline(ResiliencePipelineNames.RegisterCentre);

    private RegistrationStatus _status = RegistrationStatus.NotStarted;
    private int _consecutiveFailures;

    public override string ServiceName => "RegisterCentreClient";
    public override TimeSpan? HeartbeatInterval => null;

    public RegistrationStatus Status
    {
        get { lock (_statusLock) { return _status; } }
        private set { lock (_statusLock) { _status = value; } }
    }

    public bool IsRegistered => Status == RegistrationStatus.Completed;

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
            RecordState($"Waiting for registration timed out ({timeout})", givenLogLevel: LogLevel.Warning);
            return false;
        }
    }

    protected override async Task ExecuteBackgroundAsync(CancellationToken stoppingToken)
    {
        RecordState("Starting StateStore heartbeat loop", HostedServiceState.Starting);
        Status = RegistrationStatus.InProgress;

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
            RecordState("Service shutting down, triggering Leader lost", givenLogLevel: LogLevel.Information);
            leaderService.TriggerLeaderLost(LeaderLostReason.GracefulShutdown);

            try
            {
                await stateManager.DeleteLeaderKeyAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                RecordState("Failed to delete Leader key", exception: ex, givenLogLevel: LogLevel.Warning);
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
            await _heartbeatPipeline.ExecuteAsync(async token =>
            {
                var result = await stateManager.RegisterOrHeartbeatAsync(token);
                if (!result.Success)
                {
                    throw new HeartbeatFailedException(result.ErrorMessage ?? "Heartbeat failed");
                }
            }, ct);

            // Heartbeat succeeded (possibly after retries)
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
        if (Status != RegistrationStatus.Completed)
        {
            Status = RegistrationStatus.Completed;
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
            RecordState("Leader heartbeat failed after retries, giving up leadership", givenLogLevel: LogLevel.Warning);
            leaderService.TriggerLeaderLost(LeaderLostReason.NetworkIsolation);

            // Handle based on isolation mode
            if (_option.IsolationHandlingMode == EIsolationHandlingMode.FastShutdown)
            {
                RecordState("Isolation handling mode is FastShutdown, triggering service isolation event", givenLogLevel: LogLevel.Warning);
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
                RecordState("Leader exists, staying as Follower", givenLogLevel: LogLevel.Debug);
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
        RecordState("Attempting to compete for Leader", givenLogLevel: LogLevel.Debug);

        var (success, state, eTag) = await stateManager.TryBecomeLeaderAsync(ct);

        if (success && state != null && eTag != null)
        {
            leaderService.SetAsLeader(state.BecomeLeaderTime, eTag);
            RecordState("Successfully became Leader", HostedServiceState.Running);
        }
        else
        {
            RecordState("Leader competition failed, another instance may have become Leader", givenLogLevel: LogLevel.Debug);
        }
    }

    /// <summary>
    /// Renew Leader lease
    /// </summary>
    private async Task RenewLeaderLeaseAsync(CancellationToken ct)
    {
        var currentETag = leaderService.CurrentETag;
        if (string.IsNullOrEmpty(currentETag))
        {
            RecordState("Cannot renew Leader: ETag is empty", givenLogLevel: LogLevel.Warning);
            leaderService.TriggerLeaderLost(LeaderLostReason.NetworkIsolation);
            return;
        }

        var (success, newETag, actualState, actualETag) = await stateManager.RenewLeaderLeaseAsync(currentETag, ct);

        if (success && !string.IsNullOrEmpty(newETag))
        {
            leaderService.UpdateETag(newETag);
            RecordState("Leader lease renewed successfully", givenLogLevel: LogLevel.Debug);
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
                RecordState($"ETag mismatch but still Leader, refreshing ETag: {currentETag} -> {actualETag}", givenLogLevel: LogLevel.Information);
                leaderService.UpdateETag(actualETag);
            }
        }
        else
        {
            RecordState("Leader key expired or deleted, competing for Leader again", givenLogLevel: LogLevel.Information);
            leaderService.TriggerLeaderLost(LeaderLostReason.LeaderKeyExpired);
            await CompeteForLeaderAsync(ct);
        }
    }

    /// <summary>
    /// Internal exception for heartbeat failures
    /// </summary>
    private sealed class HeartbeatFailedException(string message) : Exception(message);
}
