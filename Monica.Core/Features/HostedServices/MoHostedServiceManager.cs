using System.Collections.Concurrent;
using Monica.Core.Extensions;
using Monica.Core.Features.HostedServices.Interfaces;
using Monica.Core.Features.HostedServices.Models;
using Monica.Core.Features.ObservableInstance;

namespace Monica.Core.Features.HostedServices;

/// <summary>
/// Implementation of IMoHostedServiceManager that tracks all registered MoHostedServices
/// </summary>
public class MoHostedServiceManager : IMoHostedServiceManager, IMoHostedServiceDependencyCoordinator
{
    private readonly ConcurrentDictionary<Type, IMoHostedService> _services = new();
    private readonly Lock _checkpointLock = new();
    private readonly Dictionary<HostedServiceCheckpointKey, HostedServiceCheckpointState> _checkpoints = [];

    /// <inheritdoc />
    public void RegisterService(IMoHostedService service)
    {
        if (!_services.TryAdd(service.GetType(), service))
        {
            return;
        }

        service.ObservableInfo.Agent.StateChanged += stateChange => OnServiceStateChanged(service.GetType(), stateChange);

        if (service.ObservableInfo.CurrentState == HostedServiceState.Faulted)
        {
            FailWaitersForFaultedService(service.GetType(), service.ObservableInfo);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<HostedServiceObservableInfo> GetAllServices()
    {
        return _services.Values.Select(s => s.ObservableInfo).ToList();
    }

    /// <inheritdoc />
    public HostedServiceObservableInfo? GetService<TService>() where TService : IMoHostedService
    {
        return GetService(typeof(TService));
    }

    /// <inheritdoc />
    public HostedServiceObservableInfo? GetService(Type serviceType)
    {
        return _services.TryGetValue(serviceType, out var service) ? service.ObservableInfo : null;
    }

    /// <inheritdoc />
    public HostedServiceObservableInfo? GetServiceByName(string serviceName)
    {
        return _services.Values
            .Select(s => s.ObservableInfo)
            .FirstOrDefault(info => info.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public IReadOnlyList<HostedServiceObservableInfo> GetServicesByState(HostedServiceState state)
    {
        return _services.Values
            .Select(s => s.ObservableInfo)
            .Where(info => info.CurrentState == state)
            .ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<HostedServiceObservableInfo> GetUnhealthyServices()
    {
        return _services.Values
            .Select(s => s.ObservableInfo)
            .Where(info => !info.IsHealthy)
            .ToList();
    }

    /// <inheritdoc />
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
                var (manager, key, pendingWaiter, token) =
                    ((MoHostedServiceManager Manager, HostedServiceCheckpointKey Key, HostedServiceCheckpointWaiter Waiter, CancellationToken Token))state!;
                manager.CancelWaiter(key, pendingWaiter, token);
            },
            (this, checkpointKey, waiter, cancellationToken));

        await waiter.CompletionSource.Task.ConfigureAwait(false);
    }

    /// <inheritdoc />
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
        if (stateChange.CurrentState is HostedServiceState.Faulted &&
            _services.TryGetValue(serviceType, out var service))
        {
            FailWaitersForFaultedService(serviceType, service.ObservableInfo);
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

    private void FailWaitersForFaultedService(Type serviceType, HostedServiceObservableInfo serviceInfo)
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
        if (!_services.TryGetValue(serviceType, out var service) ||
            service.ObservableInfo.CurrentState != HostedServiceState.Faulted)
        {
            return false;
        }

        exception = CreateFaultedServiceException(service.ObservableInfo, checkpoint);
        return true;
    }

    private static Exception CreateFaultedServiceException(
        HostedServiceObservableInfo serviceInfo,
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

    private sealed record HostedServiceCheckpointKey(Type ServiceType, string Checkpoint);

    private sealed class HostedServiceCheckpointState
    {
        public DateTime? LastOccurredAtUtc { get; set; }
        public List<HostedServiceCheckpointWaiter> Waiters { get; } = [];
    }

    private sealed class HostedServiceCheckpointWaiter(DateTime? notBeforeUtc)
    {
        public DateTime? NotBeforeUtc { get; } = notBeforeUtc;
        public TaskCompletionSource<bool> CompletionSource { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
