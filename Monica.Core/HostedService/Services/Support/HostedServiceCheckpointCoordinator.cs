using Monica.Core.Extensions;
using Monica.Core.Features.ObservableInstance;
using Monica.Core.HostedService.Abstractions;
using Monica.Core.HostedService.Abstractions.Internal;
using Monica.Core.HostedService.Models;
using Monica.Core.HostedService.Models.Internal;

namespace Monica.Core.HostedService.Services.Support;

internal sealed class HostedServiceCheckpointCoordinator(IMoHostedServiceRegistry serviceRegistry)
    : IMoHostedServiceCheckpointCoordinator, IHostedServiceCheckpointObserver
{
    private readonly Lock _checkpointLock = new();
    private readonly Dictionary<HostedServiceCheckpointKey, HostedServiceCheckpointState> _checkpoints = [];

    public void Observe(IMoHostedService service)
    {
        ArgumentNullException.ThrowIfNull(service);

        service.RuntimeInfo.Agent.StateChanged += stateChange => OnServiceStateChanged(service.GetType(), stateChange);

        if (service.RuntimeInfo.CurrentState == HostedServiceState.Faulted)
        {
            FailWaitersForFaultedService(service.GetType(), service.RuntimeInfo);
        }
    }

    public async Task WaitForCheckpointAsync<TService>(
        string checkpoint,
        DateTime? notBeforeUtc = null,
        CancellationToken cancellationToken = default)
        where TService : IMoHostedService
    {
        if (string.IsNullOrWhiteSpace(checkpoint))
        {
            throw new ArgumentException("Checkpoint name cannot be null or empty.", nameof(checkpoint));
        }

        var serviceType = typeof(TService);
        var checkpointKey = new HostedServiceCheckpointKey(serviceType, checkpoint);
        HostedServiceCheckpointWaiter? waiter = null;
        Exception? startException = null;
        var isAlreadySatisfied = false;

        lock (_checkpointLock)
        {
            var checkpointState = GetOrCreateCheckpointState(checkpointKey);
            if (checkpointState.LastOccurredAtUtc.HasValue &&
                (!notBeforeUtc.HasValue || checkpointState.LastOccurredAtUtc.Value >= notBeforeUtc.Value))
            {
                isAlreadySatisfied = true;
            }
            else if (TryCreateFaultedServiceException(serviceType, checkpoint, out startException))
            {
                isAlreadySatisfied = true;
            }
            else
            {
                waiter = new HostedServiceCheckpointWaiter(notBeforeUtc);
                checkpointState.Waiters.Add(waiter);
            }
        }

        if (startException != null)
        {
            throw startException;
        }

        if (isAlreadySatisfied || waiter == null)
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

    public void SignalCheckpoint<TService>(string checkpoint, DateTime? occurredAtUtc = null)
        where TService : IMoHostedService
    {
        SignalCheckpoint(typeof(TService), checkpoint, occurredAtUtc);
    }

    public void SignalCheckpoint(Type serviceType, string checkpoint, DateTime? occurredAtUtc = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);

        if (string.IsNullOrWhiteSpace(checkpoint))
        {
            throw new ArgumentException("Checkpoint name cannot be null or empty.", nameof(checkpoint));
        }

        var checkpointKey = new HostedServiceCheckpointKey(serviceType, checkpoint);
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

        if (waitersToRelease == null)
        {
            return;
        }

        foreach (var waiter in waitersToRelease)
        {
            waiter.CompletionSource.TrySetResult(true);
        }
    }

    private void OnServiceStateChanged(Type serviceType, ObservableStateHistory stateChange)
    {
        if (stateChange.CurrentState is not HostedServiceState.Faulted)
        {
            return;
        }

        var serviceInfo = serviceRegistry.GetService(serviceType);
        if (serviceInfo != null)
        {
            FailWaitersForFaultedService(serviceType, serviceInfo);
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

    private void FailWaitersForFaultedService(Type serviceType, HostedServiceRuntimeInfo serviceInfo)
    {
        List<(string Checkpoint, HostedServiceCheckpointWaiter Waiter)>? waitersToFail = null;

        lock (_checkpointLock)
        {
            foreach (var (checkpointKey, checkpointState) in _checkpoints)
            {
                if (checkpointKey.ServiceType != serviceType || checkpointState.Waiters.Count == 0)
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

        if (waitersToFail == null)
        {
            return;
        }

        foreach (var (checkpoint, waiter) in waitersToFail)
        {
            waiter.CompletionSource.TrySetException(CreateFaultedServiceException(serviceInfo, checkpoint));
        }
    }

    private bool TryCreateFaultedServiceException(
        Type serviceType,
        string checkpoint,
        out Exception? exception)
    {
        exception = null;
        var serviceInfo = serviceRegistry.GetService(serviceType);
        if (serviceInfo?.CurrentState != HostedServiceState.Faulted)
        {
            return false;
        }

        exception = CreateFaultedServiceException(serviceInfo, checkpoint);
        return true;
    }

    private static Exception CreateFaultedServiceException(
        HostedServiceRuntimeInfo serviceInfo,
        string checkpoint)
    {
        var lastError = serviceInfo.StateHistory
            .Where(history => history.Exception != null)
            .OrderByDescending(history => history.Timestamp)
            .FirstOrDefault()
            ?.Exception;

        var message = lastError == null
            ? $"Hosted service '{serviceInfo.ServiceName}' faulted before checkpoint '{checkpoint}' was reached."
            : $"Hosted service '{serviceInfo.ServiceName}' faulted before checkpoint '{checkpoint}' was reached: {lastError.GetMessageRecursively()}";

        return new InvalidOperationException(message, lastError);
    }
}
