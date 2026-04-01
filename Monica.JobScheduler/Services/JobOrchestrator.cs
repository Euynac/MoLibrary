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
/// Delegates actual job invocation to IMoJobExecutor while managing the complete execution workflow.
/// Lifecycle events are automatically published by JobInstanceManager during state transitions.
/// </summary>
public class JobOrchestrator(
    IServiceProvider serviceProvider,
    JobInstanceManager jobInstanceManager,
    IJobCancellationTokenManager jobCancellationManager,
    JobExecutor jobExecutor,
    JobRegistry jobRegistry,
    IOptions<ModuleJobSchedulerOption> options,
    ILogger<JobOrchestrator> logger)
{
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

        IServiceScope? scope = null;

        try
        {
            // Step 1: Create scoped service provider for job execution
            scope = serviceProvider.CreateScope();

            // Step 2: Get or create distributed cancellation token
            var jobCancellationToken = await jobCancellationManager.GetOrCreateJobTokenAsync(
                instance.InstanceId,
                cancellationToken);

            logger.LogDebug(
                "Acquired cancellation token for instance {InstanceId}",
                instance.InstanceId);

            // Step 3: Update state to Processing (will automatically publish JobStartedEvent)
            await jobInstanceManager.UpdateStateAsync(
                instance.InstanceId,
                JobState.Processing,
                null,
                cancellationToken);

            // Step 4: Execute job via executor
            var executionTask = ExecuteJobViaExecutorAsync(
                scope.ServiceProvider,
                executionEvent,
                instance,
                jobCancellationToken);

            // Step 5: Enforce timeout using Task.WhenAny
            var timeoutTask = Task.Delay(executionEvent.MaxExecutionTimeout, cancellationToken);
            var completedTask = await Task.WhenAny(executionTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                // Timeout occurred
                logger.LogWarning(
                    "Job {JobKey} instance {InstanceId} exceeded timeout of {Timeout}",
                    instance.JobKey,
                    instance.InstanceId,
                    executionEvent.MaxExecutionTimeout);

                // Cancel the distributed token (propagates to all workers if distributed)
                await jobCancellationManager.CancelJobTokenAsync(instance.InstanceId, cancellationToken);

                // Wait a brief moment for graceful cancellation
                await Task.WhenAny(executionTask, Task.Delay(TimeSpan.FromSeconds(2)));

                // Update state to Failed (will automatically publish JobCompletedEvent)
                await jobInstanceManager.UpdateStateAsync(
                    instance.InstanceId,
                    JobState.Failed,
                    $"Execution timeout after {executionEvent.MaxExecutionTimeout}",
                    cancellationToken);
            }
            else
            {
                // Job completed (either successfully or with exception)
                try
                {
                    await executionTask; // Rethrow any exception

                    logger.LogDebug(
                        "Job {JobKey} instance {InstanceId} completed successfully",
                        instance.JobKey,
                        instance.InstanceId);

                    // Update state to Succeeded (will automatically publish JobCompletedEvent)
                    await jobInstanceManager.UpdateStateAsync(
                        instance.InstanceId,
                        JobState.Succeeded,
                        null,
                        cancellationToken);
                }
                catch (OperationCanceledException) when (jobCancellationToken.IsCancellationRequested)
                {
                    // Job was cancelled via distributed cancellation (manual or timeout)
                    logger.LogWarning(
                        "Job {JobKey} instance {InstanceId} was cancelled",
                        instance.JobKey,
                        instance.InstanceId);

                    // Update state to Cancelled (will automatically publish JobCompletedEvent)
                    await jobInstanceManager.UpdateStateAsync(
                        instance.InstanceId,
                        JobState.Cancelled,
                        "Job execution was cancelled",
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    // Job threw an exception
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
                        cancellationToken);
                }
            }
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
                    cancellationToken);
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
            // Step 10: Always cleanup cancellation token
            try
            {
                await jobCancellationManager.DeleteJobTokenAsync(instance.InstanceId, CancellationToken.None);
                logger.LogDebug(
                    "Cleaned up cancellation token for instance {InstanceId}",
                    instance.InstanceId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Failed to cleanup cancellation token for instance {InstanceId}: {Message}",
                    instance.InstanceId,
                    ex.Message);
            }

            // Dispose scope
            scope?.Dispose();

            logger.LogDebug(
                "Completed execution lifecycle for job {JobKey} instance {InstanceId}",
                instance.JobKey,
                instance.InstanceId);
        }
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
