using System.Diagnostics;
using Monica.Core.Modularity.Models;

namespace Monica.Core.Modularity.Services.Support;

/// <summary>
/// Owns bounded, checkpoint-aware execution of isolated module composition work.
/// </summary>
internal sealed class ModuleCompositionWorkScheduler(int maxConcurrency) : IDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<(Type ModuleType, string Name), ModuleCompositionWorkItem> _items = [];
    private readonly PriorityQueue<ModuleCompositionWorkItem, (int Deadline, long Sequence)> _queue = new();
    private readonly SemaphoreSlim _queuedSignal = new(0);
    private readonly List<ModuleCompositionWorkCheckpointResult> _checkpoints = [];
    private readonly List<Task> _workers = [];
    private bool _accepting = true;
    private bool _disposed;
    private bool _workersStopped;
    private long _nextSequence;
    private ModuleCompositionWorkDeadline? _passedDeadline;

    /// <summary>
    /// Schedules one work item and makes it immediately eligible for bounded execution.
    /// </summary>
    internal void Schedule(
        Type moduleType,
        int registrationOrder,
        string name,
        ModulePhase originPhase,
        ModuleCompositionWorkDeadline deadline,
        Action work)
    {
        ArgumentNullException.ThrowIfNull(moduleType);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(work);
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_gate)
        {
            if (!_accepting)
            {
                throw new InvalidOperationException("Module composition work is no longer accepting new items.");
            }

            if (_passedDeadline is { } passedDeadline && deadline <= passedDeadline)
            {
                throw new InvalidOperationException(
                    $"Composition work deadline {deadline} has already passed at checkpoint {passedDeadline}.");
            }

            var identity = (moduleType, name);
            if (_items.ContainsKey(identity))
            {
                throw new InvalidOperationException(
                    $"Module {moduleType.FullName} already scheduled composition work named '{name}'.");
            }

            EnsureWorkersStarted();
            var sequence = _nextSequence++;
            var item = new ModuleCompositionWorkItem(
                moduleType,
                registrationOrder,
                name,
                originPhase,
                deadline,
                sequence,
                work);
            _items.Add(identity, item);
            _queue.Enqueue(item, ((int)deadline, sequence));
        }

        _queuedSignal.Release();
    }

    /// <summary>
    /// Waits for all work due at the next checkpoint while allowing later-deadline work to continue independently.
    /// </summary>
    internal ModuleCompositionWorkCheckpointResult ReachCheckpoint(ModuleCompositionWorkDeadline deadline)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ValidateNextCheckpoint(deadline);

        var isFinal = deadline == ModuleCompositionWorkDeadline.BeforeServiceRegistrationCompletion;
        IReadOnlyList<ModuleCompositionWorkItem> dueItems;
        int exitSignalCount;
        lock (_gate)
        {
            if (isFinal)
            {
                _accepting = false;
            }

            dueItems = GetUnreportedItems(deadline);
            exitSignalCount = isFinal ? _workers.Count : 0;
        }

        ReleaseWorkerExitSignals(exitSignalCount);
        var waitStarted = Stopwatch.GetTimestamp();
        WaitForItems(dueItems);
        var waitDuration = Stopwatch.GetElapsedTime(waitStarted);

        if (isFinal)
        {
            WaitForWorkers();
        }

        var result = new ModuleCompositionWorkCheckpointResult(
            deadline,
            dueItems.Select(static item => item.Result!).ToArray(),
            waitDuration);
        lock (_gate)
        {
            foreach (var item in dueItems)
            {
                item.MarkReported();
            }

            _passedDeadline = deadline;
            _checkpoints.Add(result);
        }

        return result;
    }

    /// <summary>
    /// Closes intake and drains every item after an unrelated phase or checkpoint failure.
    /// </summary>
    internal void AbortAndDrain()
    {
        if (_disposed)
        {
            return;
        }

        IReadOnlyList<ModuleCompositionWorkItem> remainingItems;
        int exitSignalCount;
        lock (_gate)
        {
            _accepting = false;
            if (_workersStopped)
            {
                return;
            }

            remainingItems = _items.Values
                .Where(static item => !item.IsReported)
                .OrderBy(static item => item.RegistrationOrder)
                .ThenBy(static item => item.Sequence)
                .ToArray();
            exitSignalCount = _workers.Count;
        }

        ReleaseWorkerExitSignals(exitSignalCount);
        WaitForItems(remainingItems);
        WaitForWorkers();

        lock (_gate)
        {
            foreach (var item in remainingItems)
            {
                item.MarkReported();
            }

        }
    }

    /// <summary>
    /// Returns an immutable, deterministic snapshot after all scheduled work has completed.
    /// </summary>
    internal ModuleCompositionWorkSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            if (_items.Values.Any(static item => !item.IsCompleted))
            {
                throw new InvalidOperationException(
                    "Composition work diagnostics cannot be materialized before every work item completes.");
            }

            return new ModuleCompositionWorkSnapshot(
                _items.Values
                    .OrderBy(static item => item.RegistrationOrder)
                    .ThenBy(static item => item.Sequence)
                    .Select(static item => item.Result!)
                    .ToArray(),
                _checkpoints.ToArray());
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        AbortAndDrain();
        _disposed = true;
        _queuedSignal.Dispose();
    }

    private void EnsureWorkersStarted()
    {
        if (_workers.Count > 0)
        {
            return;
        }

        if (ExecutionContext.IsFlowSuppressed())
        {
            StartWorkers();
            return;
        }

        using (ExecutionContext.SuppressFlow())
        {
            StartWorkers();
        }
    }

    private void StartWorkers()
    {
        for (var index = 0; index < maxConcurrency; index++)
        {
            _workers.Add(Task.Run(WorkerLoopAsync));
        }
    }

    private async Task WorkerLoopAsync()
    {
        while (true)
        {
            await _queuedSignal.WaitAsync().ConfigureAwait(false);

            ModuleCompositionWorkItem? item;
            lock (_gate)
            {
                if (!_queue.TryDequeue(out item, out _))
                {
                    if (!_accepting)
                    {
                        return;
                    }

                    continue;
                }

                item.MarkStarted();
            }

            item.Execute();
        }
    }

    private IReadOnlyList<ModuleCompositionWorkItem> GetUnreportedItems(
        ModuleCompositionWorkDeadline deadline)
    {
        return _items.Values
            .Where(item => !item.IsReported && item.Deadline <= deadline)
            .OrderBy(static item => item.RegistrationOrder)
            .ThenBy(static item => item.Sequence)
            .ToArray();
    }

    private void ValidateNextCheckpoint(ModuleCompositionWorkDeadline deadline)
    {
        if (!Enum.IsDefined(deadline))
        {
            throw new ArgumentOutOfRangeException(nameof(deadline), deadline, "Unknown composition work deadline.");
        }

        lock (_gate)
        {
            var expected = _passedDeadline is null
                ? ModuleCompositionWorkDeadline.BeforeBusinessTypeIteration
                : (ModuleCompositionWorkDeadline)((int)_passedDeadline.Value + 1);
            if (deadline != expected)
            {
                throw new InvalidOperationException(
                    $"Composition checkpoint {deadline} cannot run after {_passedDeadline?.ToString() ?? "initialization"}; expected {expected}.");
            }
        }
    }

    private static void WaitForItems(IReadOnlyList<ModuleCompositionWorkItem> items)
    {
        Task.WhenAll(items.Select(static item => item.Completion))
            .GetAwaiter()
            .GetResult();
    }

    private void WaitForWorkers()
    {
        lock (_gate)
        {
            if (_workersStopped)
            {
                return;
            }
        }

        Task.WhenAll(_workers).GetAwaiter().GetResult();
        lock (_gate)
        {
            _workersStopped = true;
        }
    }

    private void ReleaseWorkerExitSignals(int count)
    {
        if (count > 0)
        {
            _queuedSignal.Release(count);
        }
    }
}

/// <summary>
/// Mutable scheduler-owned work item that publishes one immutable terminal result.
/// </summary>
internal sealed class ModuleCompositionWorkItem(
    Type moduleType,
    int registrationOrder,
    string name,
    ModulePhase originPhase,
    ModuleCompositionWorkDeadline deadline,
    long sequence,
    Action work)
{
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly long _submittedTimestamp = Stopwatch.GetTimestamp();
    private readonly DateTimeOffset _submittedAtUtc = DateTimeOffset.UtcNow;
    private long _startedTimestamp;
    private DateTimeOffset _startedAtUtc;

    internal Type ModuleType { get; } = moduleType;

    internal int RegistrationOrder { get; } = registrationOrder;

    internal string Name { get; } = name;

    internal ModulePhase OriginPhase { get; } = originPhase;

    internal ModuleCompositionWorkDeadline Deadline { get; } = deadline;

    internal long Sequence { get; } = sequence;

    internal Task Completion => _completion.Task;

    internal bool IsCompleted => Result is not null;

    internal bool IsReported { get; private set; }

    internal ModuleCompositionWorkResult? Result { get; private set; }

    internal void MarkStarted()
    {
        _startedAtUtc = DateTimeOffset.UtcNow;
        _startedTimestamp = Stopwatch.GetTimestamp();
    }

    internal void Execute()
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

        var completedAtUtc = DateTimeOffset.UtcNow;
        var completedTimestamp = Stopwatch.GetTimestamp();
        Result = new ModuleCompositionWorkResult(
            ModuleType,
            RegistrationOrder,
            Name,
            OriginPhase,
            Deadline,
            Sequence,
            _startedTimestamp,
            completedTimestamp,
            _submittedAtUtc,
            _startedAtUtc,
            completedAtUtc,
            Stopwatch.GetElapsedTime(_submittedTimestamp, _startedTimestamp),
            Stopwatch.GetElapsedTime(_startedTimestamp, completedTimestamp),
            failure);
        _completion.TrySetResult();
    }

    internal void MarkReported()
    {
        IsReported = true;
    }
}

/// <summary>
/// Immutable worker-local outcome merged into diagnostics on the serial composition thread.
/// </summary>
internal sealed record ModuleCompositionWorkResult(
    Type ModuleType,
    int RegistrationOrder,
    string Name,
    ModulePhase OriginPhase,
    ModuleCompositionWorkDeadline Deadline,
    long Sequence,
    long StartedTimestamp,
    long CompletedTimestamp,
    DateTimeOffset SubmittedAtUtc,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    TimeSpan QueueDuration,
    TimeSpan ExecutionDuration,
    Exception? Failure)
{
    internal bool IsSucceeded => Failure is null;
}

/// <summary>
/// Records how long the serial composition thread waited at one checkpoint.
/// </summary>
internal sealed record ModuleCompositionWorkCheckpointResult(
    ModuleCompositionWorkDeadline Deadline,
    IReadOnlyList<ModuleCompositionWorkResult> WorkItems,
    TimeSpan WaitDuration)
{
    internal int WorkItemCount => WorkItems.Count;

    internal bool HasFailures => WorkItems.Any(static item => !item.IsSucceeded);
}

/// <summary>
/// Immutable complete scheduler result used by profiling and error aggregation.
/// </summary>
internal sealed record ModuleCompositionWorkSnapshot(
    IReadOnlyList<ModuleCompositionWorkResult> WorkItems,
    IReadOnlyList<ModuleCompositionWorkCheckpointResult> Checkpoints);

/// <summary>
/// Signals that required work failed at a composition checkpoint and composition must drain before reporting errors.
/// </summary>
internal sealed class ModuleCompositionWorkFailureException(ModuleCompositionWorkDeadline deadline)
    : Exception($"Required module composition work failed at checkpoint {deadline}.");
