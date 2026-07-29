using Monica.Core.Extensions;
using Monica.Core.HostedService.Abstractions;
using Monica.Core.HostedService.Abstractions.Internal;
using Monica.Core.HostedService.Models;
using Monica.Core.HostedService.Models.Internal;
using Monica.Core.ObservableInstance.Models;

namespace Monica.Core.HostedService.Services.Support;

internal sealed class HostedServiceCheckpointCoordinator(IMoHostedServiceRegistry serviceRegistry)
    : IMoHostedServiceCheckpointCoordinator, IHostedServiceRuntimeObserver
{
    private readonly Lock _checkpointLock = new();
    private readonly Dictionary<HostedServiceCheckpointKey, HostedServiceCheckpointState> _checkpoints = [];

    public IDisposable Observe(IMoHostedService service)
    {
        ArgumentNullException.ThrowIfNull(service);

        var runtimeInfo = service.RuntimeInfo;
        ObservableInstanceTracker.StateChangedHandler handler =
            stateChange => OnServiceStateChanged(runtimeInfo, stateChange);
        runtimeInfo.Tracker.StateChanged += handler;

        if (runtimeInfo.CurrentState == HostedServiceState.Faulted)
        {
            FailWaitersForFaultedService(runtimeInfo);
        }

        return new StateChangedSubscription(runtimeInfo.Tracker, handler);
    }

    public Task WaitForCheckpointAsync<TService>(
        string checkpoint,
        DateTime? notBeforeUtc = null,
        CancellationToken cancellationToken = default)
        where TService : IMoHostedService
    {
        var serviceInfo = ResolveExactlyOne(
            serviceRegistry.GetServices<TService>(),
            $"hosted service type '{typeof(TService).FullName}'");
        return WaitForCheckpointAsync(serviceInfo, checkpoint, notBeforeUtc, cancellationToken);
    }

    public Task WaitForCheckpointAsync<TService>(
        string serviceKey,
        string checkpoint,
        DateTime? notBeforeUtc = null,
        CancellationToken cancellationToken = default)
        where TService : IMoHostedService
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceKey);
        var serviceInfo = ResolveExactlyOne(
            serviceRegistry.GetServices<TService>()
                .Where(info => string.Equals(info.ServiceKey, serviceKey, StringComparison.Ordinal))
                .ToArray(),
            $"hosted service type '{typeof(TService).FullName}' with key '{serviceKey}'");
        return WaitForCheckpointAsync(serviceInfo, checkpoint, notBeforeUtc, cancellationToken);
    }

    public Task WaitForCheckpointAsync(
        string instanceId,
        string checkpoint,
        DateTime? notBeforeUtc = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        var serviceInfo = serviceRegistry.GetServiceByInstanceId(instanceId)
            ?? throw new InvalidOperationException(
                $"Hosted service instance '{instanceId}' is not registered in the current host.");
        return WaitForCheckpointAsync(serviceInfo, checkpoint, notBeforeUtc, cancellationToken);
    }

    public void SignalCheckpoint(
        IMoHostedService source,
        string checkpoint,
        DateTime? occurredAtUtc = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ValidateCheckpoint(checkpoint);

        var runtimeInfo = source.RuntimeInfo;
        var registered = serviceRegistry.GetServiceByInstanceId(runtimeInfo.InstanceId);
        if (!ReferenceEquals(registered, runtimeInfo))
        {
            throw new InvalidOperationException(
                $"Hosted service instance '{runtimeInfo.InstanceId}' is not registered in the current host.");
        }

        var checkpointKey = new HostedServiceCheckpointKey(runtimeInfo.InstanceId, checkpoint);
        var occurredAt = occurredAtUtc ?? DateTime.UtcNow;
        List<HostedServiceCheckpointWaiter>? waitersToRelease = null;

        lock (_checkpointLock)
        {
            var checkpointState = GetOrCreateCheckpointState(checkpointKey);
            checkpointState.LastOccurredAtUtc = occurredAt;

            for (var i = checkpointState.Waiters.Count - 1; i >= 0; i--)
            {
                var waiter = checkpointState.Waiters[i];
                if (waiter.NotBeforeUtc.HasValue && occurredAt < waiter.NotBeforeUtc.Value)
                {
                    continue;
                }

                waitersToRelease ??= [];
                waitersToRelease.Add(waiter);
                checkpointState.Waiters.RemoveAt(i);
            }
        }

        if (waitersToRelease is null)
        {
            return;
        }

        foreach (var waiter in waitersToRelease)
        {
            waiter.CompletionSource.TrySetResult(true);
        }
    }

    private async Task WaitForCheckpointAsync(
        HostedServiceRuntimeInfo serviceInfo,
        string checkpoint,
        DateTime? notBeforeUtc,
        CancellationToken cancellationToken)
    {
        ValidateCheckpoint(checkpoint);

        var checkpointKey = new HostedServiceCheckpointKey(serviceInfo.InstanceId, checkpoint);
        HostedServiceCheckpointWaiter? waiter = null;
        Exception? startException = null;
        var isAlreadySatisfied = false;

        lock (_checkpointLock)
        {
            var checkpointState = GetOrCreateCheckpointState(checkpointKey);
            if (checkpointState.LastOccurredAtUtc.HasValue
                && (!notBeforeUtc.HasValue || checkpointState.LastOccurredAtUtc.Value >= notBeforeUtc.Value))
            {
                isAlreadySatisfied = true;
            }
            else if (serviceInfo.CurrentState == HostedServiceState.Faulted)
            {
                startException = CreateFaultedServiceException(serviceInfo, checkpoint);
                isAlreadySatisfied = true;
            }
            else
            {
                waiter = new HostedServiceCheckpointWaiter(notBeforeUtc);
                checkpointState.Waiters.Add(waiter);
            }
        }

        if (startException is not null)
        {
            throw startException;
        }

        if (isAlreadySatisfied || waiter is null)
        {
            return;
        }

        using var cancellationRegistration = cancellationToken.Register(
            static state =>
            {
                var (coordinator, key, pendingWaiter, token) =
                    ((HostedServiceCheckpointCoordinator Coordinator, HostedServiceCheckpointKey Key, HostedServiceCheckpointWaiter Waiter, CancellationToken Token))state!;
                coordinator.CancelWaiter(key, pendingWaiter, token);
            },
            (this, checkpointKey, waiter, cancellationToken));

        await waiter.CompletionSource.Task.ConfigureAwait(false);
    }

    private void OnServiceStateChanged(
        HostedServiceRuntimeInfo serviceInfo,
        ObservableStateEntry stateChange)
    {
        if (stateChange.CurrentState is HostedServiceState.Faulted)
        {
            FailWaitersForFaultedService(serviceInfo);
        }
    }

    private HostedServiceCheckpointState GetOrCreateCheckpointState(HostedServiceCheckpointKey checkpointKey)
    {
        if (_checkpoints.TryGetValue(checkpointKey, out var checkpointState))
        {
            return checkpointState;
        }

        checkpointState = new HostedServiceCheckpointState();
        _checkpoints[checkpointKey] = checkpointState;
        return checkpointState;
    }

    private void CancelWaiter(
        HostedServiceCheckpointKey checkpointKey,
        HostedServiceCheckpointWaiter waiter,
        CancellationToken cancellationToken)
    {
        lock (_checkpointLock)
        {
            if (_checkpoints.TryGetValue(checkpointKey, out var checkpointState))
            {
                checkpointState.Waiters.Remove(waiter);
            }
        }

        waiter.CompletionSource.TrySetCanceled(cancellationToken);
    }

    private void FailWaitersForFaultedService(HostedServiceRuntimeInfo serviceInfo)
    {
        List<(string Checkpoint, HostedServiceCheckpointWaiter Waiter)>? waitersToFail = null;

        lock (_checkpointLock)
        {
            foreach (var (checkpointKey, checkpointState) in _checkpoints)
            {
                if (!string.Equals(checkpointKey.InstanceId, serviceInfo.InstanceId, StringComparison.Ordinal)
                    || checkpointState.Waiters.Count == 0)
                {
                    continue;
                }

                waitersToFail ??= [];
                foreach (var waiter in checkpointState.Waiters)
                {
                    waitersToFail.Add((checkpointKey.Checkpoint, waiter));
                }

                checkpointState.Waiters.Clear();
            }
        }

        if (waitersToFail is null)
        {
            return;
        }

        foreach (var (checkpoint, waiter) in waitersToFail)
        {
            waiter.CompletionSource.TrySetException(CreateFaultedServiceException(serviceInfo, checkpoint));
        }
    }

    private static HostedServiceRuntimeInfo ResolveExactlyOne(
        IReadOnlyList<HostedServiceRuntimeInfo> matches,
        string description)
    {
        return matches.Count switch
        {
            1 => matches[0],
            0 => throw new InvalidOperationException($"No {description} is registered in the current host."),
            _ => throw new InvalidOperationException(
                $"More than one {description} is registered. Select one by service key or instance ID. "
                + $"Matches: {string.Join(", ", matches.Select(FormatIdentity))}.")
        };
    }

    private static string FormatIdentity(HostedServiceRuntimeInfo serviceInfo)
    {
        return serviceInfo.ServiceKey is null
            ? serviceInfo.InstanceId
            : $"{serviceInfo.InstanceId} (key: {serviceInfo.ServiceKey})";
    }

    private static void ValidateCheckpoint(string checkpoint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(checkpoint);
    }

    private static Exception CreateFaultedServiceException(
        HostedServiceRuntimeInfo serviceInfo,
        string checkpoint)
    {
        var lastError = serviceInfo.StateHistory
            .Where(static history => history.Exception is not null)
            .OrderByDescending(static history => history.Timestamp)
            .FirstOrDefault()
            ?.Exception;

        var message = lastError is null
            ? $"Hosted service '{serviceInfo.ServiceName}' ({serviceInfo.InstanceId}) faulted before checkpoint '{checkpoint}' was reached."
            : $"Hosted service '{serviceInfo.ServiceName}' ({serviceInfo.InstanceId}) faulted before checkpoint '{checkpoint}' was reached: {lastError.GetMessageRecursively()}";

        return new InvalidOperationException(message, lastError);
    }

    private sealed class StateChangedSubscription(
        ObservableInstanceTracker tracker,
        ObservableInstanceTracker.StateChangedHandler handler) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                tracker.StateChanged -= handler;
            }
        }
    }
}
