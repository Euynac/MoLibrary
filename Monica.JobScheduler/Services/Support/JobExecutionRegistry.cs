using System.Collections.Concurrent;

namespace Monica.JobScheduler.Services.Support;

/// <summary>
/// Owns admission and lifetime tracking for worker-side job orchestration tasks.
/// </summary>
/// <remarks>
/// Closing admission and adding an execution share the same lock, so shutdown can take a stable boundary: every
/// accepted execution is visible to <see cref="DrainAsync"/>, and no execution can be admitted after the boundary.
/// </remarks>
internal sealed class JobExecutionRegistry
{
    private readonly Lock _admissionLock = new();
    private readonly ConcurrentDictionary<string, Lazy<Task>> _executions = new(StringComparer.Ordinal);
    private bool _acceptingExecutions;

    public int Count => _executions.Count;

    public void OpenAdmission()
    {
        lock (_admissionLock)
        {
            _acceptingExecutions = true;
        }
    }

    public void CloseAdmission()
    {
        lock (_admissionLock)
        {
            _acceptingExecutions = false;
        }
    }

    public bool TryStart(
        string executionId,
        Func<Task> executionFactory,
        out Task execution)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executionId);
        ArgumentNullException.ThrowIfNull(executionFactory);

        Lazy<Task> trackedExecution;
        lock (_admissionLock)
        {
            if (!_acceptingExecutions)
            {
                execution = Task.CompletedTask;
                return false;
            }

            trackedExecution = new Lazy<Task>(
                () => ExecuteAndRemoveAsync(executionId, executionFactory),
                LazyThreadSafetyMode.ExecutionAndPublication);
            if (!_executions.TryAdd(executionId, trackedExecution))
            {
                execution = Task.CompletedTask;
                return false;
            }
        }

        execution = trackedExecution.Value;
        return true;
    }

    public async Task DrainAsync(CancellationToken cancellationToken)
    {
        CloseAdmission();

        while (!_executions.IsEmpty)
        {
            var executions = _executions.Values
                .Select(static execution => execution.Value)
                .ToArray();
            if (executions.Length == 0)
            {
                continue;
            }

            await Task.WhenAll(executions).WaitAsync(cancellationToken);
        }
    }

    private async Task ExecuteAndRemoveAsync(
        string executionId,
        Func<Task> executionFactory)
    {
        try
        {
            await executionFactory();
        }
        finally
        {
            _executions.TryRemove(executionId, out _);
        }
    }
}
