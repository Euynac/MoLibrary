using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Core.Extensions;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.Events;
using Monica.JobScheduler.Models;
using Monica.Modules;

namespace Monica.JobScheduler.Services;

/// <summary>
/// Orchestrates job execution lifecycle including state management, timeout enforcement, and retry logic.
/// Delegates each job attempt to <see cref="JobExecutor"/> while managing the complete execution workflow.
/// Lifecycle events are automatically published by JobInstanceManager during state transitions.
/// </summary>
public class JobOrchestrator(
    IServiceScopeFactory serviceScopeFactory,
    JobInstanceManager jobInstanceManager,
    IJobCancellationTokenManager jobCancellationManager,
    JobExecutor jobExecutor,
    JobRegistry jobRegistry,
    IOptions<ModuleJobSchedulerOption> options,
    ILogger<JobOrchestrator> logger)
{
    private readonly ConcurrentDictionary<string, Task> _lateExecutionObservers = new(StringComparer.Ordinal);

    /// <summary>
    /// Executes a job instance with full lifecycle management.
    /// </summary>
    /// <param name="instance">The job instance to execute.</param>
    /// <param name="executionEvent">The execution event containing configuration and metadata.</param>
    /// <param name="cancellationToken">Optional cancellation token for the operation itself.</param>
    /// <returns>A task representing the execution operation.</returns>
    public async Task ExecuteAsync(
        JobInstance instance,
        JobExecutionEvent executionEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(executionEvent);

        logger.LogDebug(
            "Starting execution of job {JobKey}, InstanceId: {InstanceId}",
            instance.JobKey,
            instance.InstanceId);

        var tokenLease = new JobCancellationTokenLease(instance.InstanceId, CleanupJobTokenAsync);
        try
        {
            var jobCancellationToken = await jobCancellationManager.GetOrCreateJobTokenAsync(
                instance.InstanceId,
                cancellationToken);

            logger.LogDebug(
                "Acquired cancellation token for instance {InstanceId}",
                instance.InstanceId);

            await jobInstanceManager.UpdateStateAsync(
                instance.InstanceId,
                JobState.Processing,
                null,
                cancellationToken);

            // The execution task owns its asynchronous DI scope so timeout handling cannot release job dependencies
            // while user code is still running.
            var executionTask = ExecuteJobInScopeAsync(
                executionEvent,
                instance,
                jobCancellationToken);

            try
            {
                await executionTask.WaitAsync(
                    executionEvent.MaxExecutionTimeout,
                    cancellationToken);

                logger.LogDebug(
                    "Job {JobKey} instance {InstanceId} completed successfully",
                    instance.JobKey,
                    instance.InstanceId);

                await jobInstanceManager.UpdateStateAsync(
                    instance.InstanceId,
                    JobState.Succeeded,
                    cancellationToken: CancellationToken.None);
            }
            catch (TimeoutException) when (cancellationToken.IsCancellationRequested)
            {
                await HandleHostCancellationAsync(instance, executionTask, tokenLease);
                throw new OperationCanceledException(
                    "Host operation was cancelled while the job deadline was expiring.",
                    cancellationToken);
            }
            catch (TimeoutException) when (!executionTask.IsCompleted)
            {
                await HandleTimeoutAsync(
                    instance,
                    executionEvent.MaxExecutionTimeout,
                    executionTask,
                    tokenLease);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                await HandleHostCancellationAsync(instance, executionTask, tokenLease);
                throw;
            }
            catch (OperationCanceledException) when (jobCancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(
                    "Job {JobKey} instance {InstanceId} was cancelled",
                    instance.JobKey,
                    instance.InstanceId);

                await jobInstanceManager.UpdateStateAsync(
                    instance.InstanceId,
                    JobState.Cancelled,
                    "Job execution was cancelled",
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Job {JobKey} instance {InstanceId} failed with exception: {Message}",
                    instance.JobKey,
                    instance.InstanceId,
                    ex.GetMessageRecursively());

                await jobInstanceManager.UpdateStateAsync(
                    instance.InstanceId,
                    JobState.Failed,
                    $"{ex}",
                    CancellationToken.None);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation(
                "Job {JobKey} instance {InstanceId} execution was abandoned because the host operation was cancelled",
                instance.JobKey,
                instance.InstanceId);
            throw;
        }
        catch (Exception ex)
        {
            // Catch-all for any unexpected errors in the orchestrator itself
            logger.LogError(
                ex,
                "Critical error in JobOrchestrator for {JobKey} instance {InstanceId}: {Message}",
                instance.JobKey,
                instance.InstanceId,
                ex.GetMessageRecursively());

            try
            {
                // Update state to Failed (will automatically publish JobCompletedEvent)
                await jobInstanceManager.UpdateStateAsync(
                    instance.InstanceId,
                    JobState.Failed,
                    $"Orchestrator error: {ex}",
                    CancellationToken.None);
            }
            catch (Exception updateEx)
            {
                logger.LogError(
                    updateEx,
                    "Failed to update job state after orchestrator error: {Message}",
                    updateEx.Message);
            }
        }
        finally
        {
            await tokenLease.DisposeAsync();

            logger.LogDebug(
                "Completed execution lifecycle for job {JobKey} instance {InstanceId}",
                instance.JobKey,
                instance.InstanceId);
        }
    }

    /// <summary>
    /// Waits for cancellation-ignoring executions that outlived their finalized scheduler operation.
    /// </summary>
    /// <remarks>
    /// The worker host calls this during shutdown after normal orchestration tasks have stopped. The wait is bounded by
    /// the host shutdown token; jobs that ignore cancellation beyond that boundary are reported by the worker as a
    /// degraded shutdown.
    /// </remarks>
    internal async Task DrainLateExecutionsAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var observers = _lateExecutionObservers.Values.ToArray();
            if (observers.Length == 0)
            {
                return;
            }

            await Task.WhenAll(observers).WaitAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Waits until a job execution that ignored scheduler cancellation has released its execution scope.
    /// </summary>
    /// <remarks>
    /// Worker concurrency slots use this completion boundary so a timed-out job still counts as executing until its
    /// user code actually exits. Completed or cancellation-cooperative jobs return immediately.
    /// </remarks>
    internal Task WaitForExecutionReleaseAsync(string instanceId)
    {
        return _lateExecutionObservers.GetValueOrDefault(instanceId) ?? Task.CompletedTask;
    }

    private async Task HandleTimeoutAsync(
        JobInstance instance,
        TimeSpan timeout,
        Task executionTask,
        JobCancellationTokenLease tokenLease)
    {
        logger.LogWarning(
            "Job {JobKey} instance {InstanceId} exceeded timeout of {Timeout}",
            instance.JobKey,
            instance.InstanceId,
            timeout);

        await TryCancelJobTokenAsync(instance.InstanceId);
        var completedDuringGracePeriod = await WaitForGracefulCompletionAsync(executionTask);
        if (!completedDuringGracePeriod)
        {
            tokenLease.TransferToLateExecution();
            TrackLateExecution(instance, executionTask, "after timeout", tokenLease);
        }

        await jobInstanceManager.UpdateStateAsync(
            instance.InstanceId,
            JobState.Failed,
            $"Execution timeout after {timeout}",
            CancellationToken.None);

        if (completedDuringGracePeriod)
        {
            await ObserveCompletedExecutionAsync(instance, executionTask, "after timeout cancellation");
        }
    }

    private async Task HandleHostCancellationAsync(
        JobInstance instance,
        Task executionTask,
        JobCancellationTokenLease tokenLease)
    {
        logger.LogInformation(
            "Host cancellation requested for job {JobKey} instance {InstanceId}",
            instance.JobKey,
            instance.InstanceId);

        await TryCancelJobTokenAsync(instance.InstanceId);
        var completedDuringGracePeriod = await WaitForGracefulCompletionAsync(executionTask);
        if (!completedDuringGracePeriod)
        {
            tokenLease.TransferToLateExecution();
            TrackLateExecution(instance, executionTask, "after host cancellation", tokenLease);
        }

        await jobInstanceManager.UpdateStateAsync(
            instance.InstanceId,
            JobState.Cancelled,
            "Host operation was cancelled",
            CancellationToken.None);

        if (completedDuringGracePeriod)
        {
            await ObserveCompletedExecutionAsync(instance, executionTask, "after host cancellation");
        }
    }

    private async Task<bool> WaitForGracefulCompletionAsync(Task executionTask)
    {
        var gracePeriod = options.Value.ExecutionCancellationGracePeriod;
        if (gracePeriod <= TimeSpan.Zero)
        {
            return executionTask.IsCompleted;
        }

        return await Task.WhenAny(
            executionTask,
            Task.Delay(gracePeriod, CancellationToken.None)) == executionTask;
    }

    private void TrackLateExecution(
        JobInstance instance,
        Task executionTask,
        string completionContext,
        JobCancellationTokenLease tokenLease)
    {
        var observer = ObserveLateExecutionAsync(instance, executionTask, completionContext, tokenLease);
        if (!_lateExecutionObservers.TryAdd(instance.InstanceId, observer))
        {
            throw new InvalidOperationException(
                $"A late execution is already tracked for job instance '{instance.InstanceId}'.");
        }

        observer.GetAwaiter().OnCompleted(() =>
            _lateExecutionObservers.TryRemove(instance.InstanceId, out _));

        logger.LogWarning(
            "Job {JobKey} instance {InstanceId} is still running {CompletionContext}; its scope and cancellation token remain tracked",
            instance.JobKey,
            instance.InstanceId,
            completionContext);
    }

    private async Task ObserveLateExecutionAsync(
        JobInstance instance,
        Task executionTask,
        string completionContext,
        JobCancellationTokenLease tokenLease)
    {
        try
        {
            await ObserveCompletedExecutionAsync(instance, executionTask, completionContext);
        }
        finally
        {
            await tokenLease.CompleteLateExecutionAsync();
        }
    }

    private async Task ObserveCompletedExecutionAsync(
        JobInstance instance,
        Task executionTask,
        string completionContext)
    {
        try
        {
            await executionTask;
            logger.LogWarning(
                "Job {JobKey} instance {InstanceId} completed {CompletionContext} after the scheduler stopped waiting",
                instance.JobKey,
                instance.InstanceId,
                completionContext);
        }
        catch (OperationCanceledException)
        {
            logger.LogDebug(
                "Job {JobKey} instance {InstanceId} acknowledged cancellation {CompletionContext}",
                instance.JobKey,
                instance.InstanceId,
                completionContext);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Job {JobKey} instance {InstanceId} failed late {CompletionContext}: {Message}",
                instance.JobKey,
                instance.InstanceId,
                completionContext,
                ex.GetMessageRecursively());
        }
    }

    private async Task TryCancelJobTokenAsync(string instanceId)
    {
        try
        {
            await jobCancellationManager.CancelJobTokenAsync(instanceId, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to signal cancellation for job instance {InstanceId}: {Message}",
                instanceId,
                ex.Message);
        }
    }

    private async Task CleanupJobTokenAsync(string instanceId)
    {
        try
        {
            await jobCancellationManager.DeleteJobTokenAsync(instanceId, CancellationToken.None);
            logger.LogDebug(
                "Cleaned up cancellation token for instance {InstanceId}",
                instanceId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to cleanup cancellation token for instance {InstanceId}: {Message}",
                instanceId,
                ex.Message);
        }
    }

    private sealed class JobCancellationTokenLease(
        string instanceId,
        Func<string, Task> cleanup) : IAsyncDisposable
    {
        private const int ORCHESTRATOR_OWNER = 0;
        private const int LATE_EXECUTION_OWNER = 1;
        private const int CLEANED = 2;
        private int _owner = ORCHESTRATOR_OWNER;

        public void TransferToLateExecution()
        {
            if (Interlocked.CompareExchange(
                    ref _owner,
                    LATE_EXECUTION_OWNER,
                    ORCHESTRATOR_OWNER) != ORCHESTRATOR_OWNER)
            {
                throw new InvalidOperationException(
                    $"Cancellation-token cleanup for job instance '{instanceId}' has already been transferred or completed.");
            }
        }

        public async Task CompleteLateExecutionAsync()
        {
            if (Interlocked.CompareExchange(ref _owner, CLEANED, LATE_EXECUTION_OWNER)
                == LATE_EXECUTION_OWNER)
            {
                await cleanup(instanceId);
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.CompareExchange(ref _owner, CLEANED, ORCHESTRATOR_OWNER)
                == ORCHESTRATOR_OWNER)
            {
                await cleanup(instanceId);
            }
        }
    }

    private async Task ExecuteJobInScopeAsync(
        JobExecutionEvent executionEvent,
        JobInstance instance,
        CancellationToken cancellationToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        await ExecuteJobViaExecutorAsync(
            scope.ServiceProvider,
            executionEvent,
            instance,
            cancellationToken);
    }

    /// <summary>
    /// Executes the job by delegating to the appropriate executor method based on job type.
    /// </summary>
    private async Task ExecuteJobViaExecutorAsync(
        IServiceProvider scopedProvider,
        JobExecutionEvent executionEvent,
        JobInstance instance,
        CancellationToken cancellationToken)
    {
        // Get CLR type from JobRegistry
        var jobType = jobRegistry.GetJobClrType(executionEvent.JobKey);
        if (jobType == null)
        {
            throw new InvalidOperationException(
                $"Job type not found in registry for JobKey: {executionEvent.JobKey}");
        }
        
        // Deserialize job arguments for triggered jobs
        object? jobArgs = null;
        if (executionEvent.JobType == JobType.Triggered)
        {
            var argsType = jobRegistry.GetJobArgsClrType(executionEvent.JobArgsKey!);
            if (argsType != null)
            {
                try
                {
                    if (string.IsNullOrEmpty(executionEvent.JobArgs))
                    {
                        throw new InvalidOperationException($"Job {executionEvent.JobKey} arguments cannot be null or empty");
                    }
                    jobArgs = JsonSerializer.Deserialize(executionEvent.JobArgs, argsType, options.Value.JobArgsSerializerOptions);
                }
                catch (JsonException ex)
                {
                    logger.LogError(
                        ex,
                        "Failed to deserialize job arguments for {JobKey} instance {InstanceId}",
                        instance.JobKey,
                        instance.InstanceId);

                    throw new InvalidOperationException(
                        $"Failed to deserialize job arguments: {ex.Message}", ex);
                }
            }
        }

        // Create execution context
        var context = new JobExecutionContext
        {
            InstanceId = instance.InstanceId,
            ServiceProvider = scopedProvider,
            JobType = jobType,
            JobArgs = jobArgs,
            CancellationToken = cancellationToken
        };

        // Delegate to appropriate executor method based on job type
        if (executionEvent.JobType == JobType.Recurring)
        {
            logger.LogDebug(
                "Executing recurring job {JobKey} instance {InstanceId}",
                instance.JobKey,
                instance.InstanceId);

            await jobExecutor.ExecuteRecurringJobAsync(context);
        }
        else if (executionEvent.JobType == JobType.Triggered)
        {
            logger.LogDebug(
                "Executing triggered job {JobKey} instance {InstanceId}",
                instance.JobKey,
                instance.InstanceId);

            await jobExecutor.ExecuteTriggeredJobAsync(context);
        }
        else
        {
            throw new InvalidOperationException(
                $"Job type {jobType.FullName} is not a supported job type");
        }

        logger.LogDebug(
            "Job execution completed for {JobKey} instance {InstanceId}",
            instance.JobKey,
            instance.InstanceId);
    }
}
