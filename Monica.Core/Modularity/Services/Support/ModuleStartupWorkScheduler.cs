using System.Diagnostics;
using Monica.Core.Modularity.Models;

namespace Monica.Core.Modularity.Services.Support;

/// <summary>
/// Owns bounded execution of module startup work across composition and host-lifecycle barriers.
/// </summary>
internal sealed class ModuleStartupWorkScheduler(
    int maxConcurrency,
    Action<ModuleStartupWorkResult>? itemCompleted = null) : IDisposable
{
    private readonly object _gate = new();
    private readonly object _completionSignalGate = new();
    private readonly Dictionary<(Type ModuleType, string Name), ModuleStartupWorkItem> _items = [];
    private readonly PriorityQueue<ModuleStartupWorkItem, (int Barrier, long Sequence)> _queue = new();
    private readonly List<ModuleStartupWorkBarrierResult> _barriers = [];
    private readonly Dictionary<ModuleStartupWorkBarrier, TaskCompletionSource<ModuleStartupWorkBarrierResult>>
        _barrierReleases = [];
    private readonly TaskCompletionSource _drained = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _activeCount;
    private int _activeNoBarrierCount;
    private bool _accepting = true;
    private bool _disposed;
    private bool _drainStarted;
    private long _nextCompletionSignalSequence;
    private long _nextSequence;
    private ModuleStartupWorkBarrier? _passedBarrier;

    /// <summary>
    /// Schedules one work item and makes it immediately eligible for bounded execution.
    /// </summary>
    internal void Schedule(
        Type moduleType,
        ModuleKey moduleKey,
        int registrationOrder,
        string name,
        ModulePhase originPhase,
        ModuleStartupWorkBarrier barrier,
        Action work,
        Action? commit = null)
    {
        ArgumentNullException.ThrowIfNull(moduleType);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(work);
        ObjectDisposedException.ThrowIf(_disposed, this);

        IReadOnlyList<ModuleStartupWorkItem> dispatch;
        lock (_gate)
        {
            if (!_accepting)
            {
                throw new InvalidOperationException("Module startup work is no longer accepting new items.");
            }

            if (_passedBarrier is { } passedBarrier
                && barrier != ModuleStartupWorkBarrier.NoBarrier
                && barrier <= passedBarrier)
            {
                throw new InvalidOperationException(
                    $"Startup work barrier {barrier} has already passed at {passedBarrier}.");
            }

            var identity = (moduleType, name);
            if (_items.ContainsKey(identity))
            {
                throw new InvalidOperationException(
                    $"Module {moduleType.FullName} already scheduled startup work named '{name}'.");
            }

            var sequence = _nextSequence++;
            var item = new ModuleStartupWorkItem(
                moduleType,
                moduleKey,
                registrationOrder,
                name,
                originPhase,
                barrier,
                sequence,
                work,
                commit);
            _items.Add(identity, item);
            _queue.Enqueue(item, ((int)barrier, sequence));
            dispatch = ReserveAvailableWorkUnderLock();
        }

        StartExecutions(dispatch);
    }

    /// <summary>
    /// Closes submission after the last post-service callback while allowing queued work to continue.
    /// </summary>
    internal void CloseSubmissions()
    {
        IReadOnlyList<ModuleStartupWorkItem> dispatch;
        lock (_gate)
        {
            if (!_accepting)
            {
                return;
            }

            _accepting = false;
            dispatch = ReserveAvailableWorkUnderLock();
        }

        StartExecutions(dispatch);
    }

    /// <summary>
    /// Waits for required work governed by the next ordered startup barrier.
    /// </summary>
    internal ModuleStartupWorkBarrierResult ReachBarrier(
        ModuleStartupWorkBarrier barrier,
        Action<ModuleStartupWorkBarrier>? barrierEntered = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        TaskCompletionSource<ModuleStartupWorkBarrierResult> release;
        var ownsRelease = false;
        IReadOnlyList<ModuleStartupWorkItem> dueItems;
        lock (_gate)
        {
            if (_barrierReleases.TryGetValue(barrier, out var existingRelease))
            {
                release = existingRelease;
                dueItems = [];
            }
            else
            {
                ValidateNextBarrierUnderLock(barrier);
                release = new TaskCompletionSource<ModuleStartupWorkBarrierResult>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                _barrierReleases.Add(barrier, release);
                ownsRelease = true;
                dueItems = GetUnreportedItems(barrier);
            }
        }

        if (!ownsRelease)
        {
            return release.Task.GetAwaiter().GetResult();
        }

        try
        {
            var enteredAtUtc = DateTimeOffset.UtcNow;
            var enteredTimestamp = Stopwatch.GetTimestamp();
            barrierEntered?.Invoke(barrier);
            WaitForItems(dueItems);

            var releasedAtUtc = DateTimeOffset.UtcNow;
            var releasedTimestamp = Stopwatch.GetTimestamp();
            var dueResults = dueItems.Select(static item => item.CreateSnapshot()).ToArray();
            var pendingWorkItems = dueResults
                .Where(result => result.CompletionSignaledTimestamp is { } signaledTimestamp
                                 && signaledTimestamp > enteredTimestamp)
                .Select(result => new ModuleStartupWorkBarrierPendingResult(
                    result.WorkItemId,
                    Stopwatch.GetElapsedTime(enteredTimestamp, result.CompletionSignaledTimestamp!.Value)))
                .ToArray();
            var releasingWorkItemId = dueResults
                .Where(result => result.CompletionSignaledTimestamp is { } signaledTimestamp
                                 && signaledTimestamp > enteredTimestamp)
                .OrderBy(static result => result.CompletionSignalSequence)
                .Select(static result => result.WorkItemId)
                .LastOrDefault();

            ModuleStartupWorkBarrierResult result;
            lock (_gate)
            {
                result = new ModuleStartupWorkBarrierResult(
                    barrier,
                    _barriers.Count,
                    enteredTimestamp,
                    releasedTimestamp,
                    enteredAtUtc,
                    releasedAtUtc,
                    dueResults,
                    pendingWorkItems,
                    releasingWorkItemId);
                foreach (var item in dueItems)
                {
                    item.MarkReported();
                }

                _passedBarrier = barrier;
                _barriers.Add(result);
            }

            release.TrySetResult(result);
            return result;
        }
        catch (Exception exception)
        {
            release.TrySetException(exception);
            throw;
        }
    }

    /// <summary>
    /// Drains every remaining work item without promoting diagnostic-only failures to host failures.
    /// </summary>
    internal void Drain()
    {
        Task? existingDrain = null;
        lock (_gate)
        {
            if (_drainStarted)
            {
                existingDrain = _drained.Task;
            }
            else
            {
                _drainStarted = true;
            }
        }

        if (existingDrain is not null)
        {
            existingDrain.GetAwaiter().GetResult();
            return;
        }

        try
        {
            CloseSubmissions();
            ModuleStartupWorkItem[] items;
            lock (_gate)
            {
                items = _items.Values.ToArray();
            }

            WaitForItems(items);
            _drained.TrySetResult();
        }
        catch (Exception exception)
        {
            _drained.TrySetException(exception);
            throw;
        }
    }

    /// <summary>
    /// Returns a thread-safe live snapshot, including queued and running work.
    /// </summary>
    internal ModuleStartupWorkSnapshot GetSnapshot()
    {
        var observedTimestamp = Stopwatch.GetTimestamp();
        var observedAtUtc = DateTimeOffset.UtcNow;
        lock (_gate)
        {
            return new ModuleStartupWorkSnapshot(
                _items.Values
                    .OrderBy(static item => item.RegistrationOrder)
                    .ThenBy(static item => item.Sequence)
                    .Select(item => item.CreateSnapshot(observedTimestamp, observedAtUtc))
                    .ToArray(),
                _barriers.ToArray());
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }
        }

        Drain();
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }
    }

    /// <summary>
    /// Starts as much queued work as the global concurrency limit permits.
    /// </summary>
    /// <remarks>
    /// While modules may still submit work, non-blocking items can occupy at most all but one lane. The reserved lane
    /// guarantees that a later required item can begin even when earlier non-blocking work is long-running. With a
    /// single configured lane, non-blocking items remain queued until submissions close. Once closed, all required
    /// items are known and the priority queue dispatches them before releasing remaining capacity to non-blocking work.
    /// </remarks>
    private IReadOnlyList<ModuleStartupWorkItem> ReserveAvailableWorkUnderLock()
    {
        List<ModuleStartupWorkItem>? dispatch = null;
        while (_activeCount < maxConcurrency && TryDequeueEligibleWorkUnderLock(out var item))
        {
            _activeCount++;
            if (item.Barrier == ModuleStartupWorkBarrier.NoBarrier)
            {
                _activeNoBarrierCount++;
            }

            (dispatch ??= []).Add(item);
        }

        return dispatch ?? [];
    }

    private bool TryDequeueEligibleWorkUnderLock(out ModuleStartupWorkItem item)
    {
        if (!_queue.TryPeek(out var next, out _))
        {
            item = null!;
            return false;
        }

        if (next.Barrier == ModuleStartupWorkBarrier.NoBarrier && _accepting)
        {
            var availableNoBarrierLanes = Math.Max(0, maxConcurrency - 1);
            if (_activeNoBarrierCount >= availableNoBarrierLanes)
            {
                item = null!;
                return false;
            }
        }

        _queue.Dequeue();
        item = next;
        return true;
    }

    private void StartExecutions(IReadOnlyList<ModuleStartupWorkItem> items)
    {
        if (items.Count == 0)
        {
            return;
        }

        if (ExecutionContext.IsFlowSuppressed())
        {
            StartExecutionsWithoutContext(items);
            return;
        }

        using (ExecutionContext.SuppressFlow())
        {
            StartExecutionsWithoutContext(items);
        }
    }

    private void StartExecutionsWithoutContext(IReadOnlyList<ModuleStartupWorkItem> items)
    {
        foreach (var item in items)
        {
            _ = Task.Run(() => ExecuteItem(item));
        }
    }

    private void ExecuteItem(ModuleStartupWorkItem item)
    {
        item.MarkStarted();
        var result = item.Execute();
        try
        {
            itemCompleted?.Invoke(result);
        }
        catch
        {
            // Diagnostic observers must never terminate a bounded startup worker.
        }

        IReadOnlyList<ModuleStartupWorkItem> dispatch;
        lock (_gate)
        {
            _activeCount--;
            if (item.Barrier == ModuleStartupWorkBarrier.NoBarrier)
            {
                _activeNoBarrierCount--;
            }

            dispatch = ReserveAvailableWorkUnderLock();
        }

        StartExecutions(dispatch);
        lock (_completionSignalGate)
        {
            item.SignalCompletion(_nextCompletionSignalSequence++);
        }
    }

    private IReadOnlyList<ModuleStartupWorkItem> GetUnreportedItems(ModuleStartupWorkBarrier barrier)
    {
        return _items.Values
            .Where(item => !item.IsReported
                           && item.Barrier != ModuleStartupWorkBarrier.NoBarrier
                           && item.Barrier <= barrier)
            .OrderBy(static item => item.RegistrationOrder)
            .ThenBy(static item => item.Sequence)
            .ToArray();
    }

    private void ValidateNextBarrierUnderLock(ModuleStartupWorkBarrier barrier)
    {
        if (!Enum.IsDefined(barrier) || barrier == ModuleStartupWorkBarrier.NoBarrier)
        {
            throw new ArgumentOutOfRangeException(nameof(barrier), barrier, "Unknown or non-blocking startup barrier.");
        }

        var expected = _passedBarrier is null
            ? ModuleStartupWorkBarrier.BeforeTypeDiscovery
            : (ModuleStartupWorkBarrier)((int)_passedBarrier.Value + 1);
        if (barrier != expected)
        {
            throw new InvalidOperationException(
                $"Startup work barrier {barrier} cannot run after {_passedBarrier?.ToString() ?? "initialization"}; " +
                $"expected {expected}.");
        }
    }

    private static void WaitForItems(IReadOnlyList<ModuleStartupWorkItem> items)
    {
        Task.WhenAll(items.Select(static item => item.Completion)).GetAwaiter().GetResult();
    }

}

/// <summary>
/// Mutable scheduler-owned work item that can publish a live immutable observation.
/// </summary>
internal sealed class ModuleStartupWorkItem(
    Type moduleType,
    ModuleKey moduleKey,
    int registrationOrder,
    string name,
    ModulePhase originPhase,
    ModuleStartupWorkBarrier barrier,
    long sequence,
    Action work,
    Action? commit)
{
    private readonly object _gate = new();
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly long _submittedTimestamp = Stopwatch.GetTimestamp();
    private readonly DateTimeOffset _submittedAtUtc = DateTimeOffset.UtcNow;
    private long? _startedTimestamp;
    private DateTimeOffset? _startedAtUtc;
    private long? _completedTimestamp;
    private DateTimeOffset? _completedAtUtc;
    private long? _completionSignaledTimestamp;
    private long? _completionSignalSequence;
    private Exception? _failure;
    private bool _isReported;
    private ModuleStartupWorkExecutionStatus _status = ModuleStartupWorkExecutionStatus.Queued;

    internal Type ModuleType { get; } = moduleType;

    internal ModuleKey ModuleKey { get; } = moduleKey;

    internal int RegistrationOrder { get; } = registrationOrder;

    internal string Name { get; } = name;

    internal ModulePhase OriginPhase { get; } = originPhase;

    internal ModuleStartupWorkBarrier Barrier { get; } = barrier;

    internal long Sequence { get; } = sequence;

    internal string WorkItemId { get; } = $"startup-work-{sequence:D6}";

    internal Task Completion => _completion.Task;

    internal bool IsReported
    {
        get
        {
            lock (_gate)
            {
                return _isReported;
            }
        }
    }

    internal void MarkStarted()
    {
        lock (_gate)
        {
            _startedAtUtc = DateTimeOffset.UtcNow;
            _startedTimestamp = Stopwatch.GetTimestamp();
            _status = ModuleStartupWorkExecutionStatus.Running;
        }
    }

    internal ModuleStartupWorkResult Execute()
    {
        Exception? failure = null;
        try
        {
            work();
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        ModuleStartupWorkResult result;
        lock (_gate)
        {
            _completedAtUtc = DateTimeOffset.UtcNow;
            _completedTimestamp = Stopwatch.GetTimestamp();
            _failure = failure;
            _status = failure is null
                ? ModuleStartupWorkExecutionStatus.Succeeded
                : ModuleStartupWorkExecutionStatus.Failed;
            result = CreateSnapshotCore(_completedTimestamp.Value, _completedAtUtc.Value);
        }

        return result;
    }

    internal void SignalCompletion(long signalSequence)
    {
        lock (_gate)
        {
            _completionSignaledTimestamp = Stopwatch.GetTimestamp();
            _completionSignalSequence = signalSequence;
            _completion.TrySetResult();
        }
    }

    internal ModuleStartupWorkResult CreateSnapshot()
    {
        return CreateSnapshot(Stopwatch.GetTimestamp(), DateTimeOffset.UtcNow);
    }

    internal ModuleStartupWorkResult CreateSnapshot(long observedTimestamp, DateTimeOffset observedAtUtc)
    {
        lock (_gate)
        {
            return CreateSnapshotCore(observedTimestamp, observedAtUtc);
        }
    }

    internal void MarkReported()
    {
        lock (_gate)
        {
            _isReported = true;
        }
    }

    private ModuleStartupWorkResult CreateSnapshotCore(long observedTimestamp, DateTimeOffset observedAtUtc)
    {
        var queueEnd = _startedTimestamp ?? observedTimestamp;
        var executionEnd = _completedTimestamp ?? observedTimestamp;
        return new ModuleStartupWorkResult(
            WorkItemId,
            ModuleType,
            ModuleKey,
            RegistrationOrder,
            Name,
            OriginPhase,
            Barrier,
            Sequence,
            _status,
            _submittedTimestamp,
            _startedTimestamp,
            _completedTimestamp,
            _submittedAtUtc,
            _startedAtUtc,
            _completedAtUtc,
            _completionSignaledTimestamp,
            _completionSignalSequence,
            observedTimestamp,
            observedAtUtc,
            Stopwatch.GetElapsedTime(_submittedTimestamp, queueEnd),
            _startedTimestamp is null
                ? TimeSpan.Zero
                : Stopwatch.GetElapsedTime(_startedTimestamp.Value, executionEnd),
            _failure,
            commit);
    }
}

internal enum ModuleStartupWorkExecutionStatus
{
    Queued,
    Running,
    Succeeded,
    Failed
}

/// <summary>
/// Immutable live or terminal startup-work observation.
/// </summary>
internal sealed record ModuleStartupWorkResult(
    string WorkItemId,
    Type ModuleType,
    ModuleKey ModuleKey,
    int RegistrationOrder,
    string Name,
    ModulePhase OriginPhase,
    ModuleStartupWorkBarrier Barrier,
    long Sequence,
    ModuleStartupWorkExecutionStatus Status,
    long SubmittedTimestamp,
    long? StartedTimestamp,
    long? CompletedTimestamp,
    DateTimeOffset SubmittedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    long? CompletionSignaledTimestamp,
    long? CompletionSignalSequence,
    long ObservedTimestamp,
    DateTimeOffset ObservedAtUtc,
    TimeSpan QueueDuration,
    TimeSpan ExecutionDuration,
    Exception? Failure,
    Action? Commit)
{
    internal bool IsTerminal => Status is ModuleStartupWorkExecutionStatus.Succeeded
        or ModuleStartupWorkExecutionStatus.Failed;

    internal bool IsSucceeded => Status == ModuleStartupWorkExecutionStatus.Succeeded;
}

/// <summary>
/// Records how long the serial control plane waited at one startup-work barrier.
/// </summary>
internal sealed record ModuleStartupWorkBarrierResult(
    ModuleStartupWorkBarrier Barrier,
    long Sequence,
    long EnteredTimestamp,
    long ReleasedTimestamp,
    DateTimeOffset EnteredAtUtc,
    DateTimeOffset ReleasedAtUtc,
    IReadOnlyList<ModuleStartupWorkResult> WorkItems,
    IReadOnlyList<ModuleStartupWorkBarrierPendingResult> PendingWorkItems,
    string? ReleasingWorkItemId)
{
    internal bool HasFailures => WorkItems.Any(static item => !item.IsSucceeded);
}

/// <summary>
/// Records the exact remaining duration of one item that was pending at barrier entry.
/// </summary>
internal sealed record ModuleStartupWorkBarrierPendingResult(
    string WorkItemId,
    TimeSpan RemainingDuration);

/// <summary>
/// Immutable live scheduler snapshot used by profiling and error aggregation.
/// </summary>
internal sealed record ModuleStartupWorkSnapshot(
    IReadOnlyList<ModuleStartupWorkResult> WorkItems,
    IReadOnlyList<ModuleStartupWorkBarrierResult> Barriers);

/// <summary>
/// Signals that required startup work failed at a barrier.
/// </summary>
internal sealed class ModuleStartupWorkFailureException : Exception
{
    internal ModuleStartupWorkFailureException(
        ModuleStartupWorkBarrier barrier,
        IReadOnlyList<ModuleStartupWorkResult> failures)
        : base(
            CreateMessage(barrier, failures),
            new AggregateException(failures.Select(static failure => failure.Failure!).ToArray()))
    {
    }

    private static string CreateMessage(
        ModuleStartupWorkBarrier barrier,
        IReadOnlyList<ModuleStartupWorkResult> failures)
    {
        var names = string.Join(", ", failures.Select(static failure => failure.Name));
        return $"Required module startup work failed at barrier {barrier}: {names}.";
    }
}
