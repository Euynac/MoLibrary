using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoLibrary.EventBus.Abstractions;
using MoLibrary.StateStore.CancellationManager;
using MoLibrary.JobScheduler.Abstractions;
using MoLibrary.JobScheduler.Events;
using MoLibrary.JobScheduler.Models;
using MoLibrary.JobScheduler.Jobs;

namespace MoLibrary.JobScheduler.WorkerPlane;

/// <summary>
/// Executes individual job instances with dependency injection, timeout handling, and retry logic.
/// Manages the complete job execution lifecycle including DI resolution, distributed cancellation,
/// state updates, and cleanup.
/// </summary>
/// <remarks>
/// <para>
/// JobExecutor is responsible for the actual execution of job instances. It:
/// </para>
/// <list type="bullet">
/// <item><description>Creates scoped service providers for isolated job execution</description></item>
/// <item><description>Resolves job instances via dependency injection</description></item>
/// <item><description>Manages distributed cancellation tokens via IMoCancellationManager</description></item>
/// <item><description>Enforces timeouts using Task.WhenAny pattern</description></item>
/// <item><description>Implements retry logic for failed jobs</description></item>
/// <item><description>Updates job state throughout the execution lifecycle</description></item>
/// <item><description>Ensures proper cleanup of cancellation tokens</description></item>
/// </list>
/// </remarks>
public class JobExecutor(
    IServiceProvider serviceProvider,
    IMoJobScheduleMetadataStore metadataStore,
    IMoCancellationManager cancellationManager,
    IMoEventBus eventBus,
    ILogger<JobExecutor> logger)
{
    /// <summary>
    /// Executes a job instance with full lifecycle management.
    /// </summary>
    /// <param name="instance">The job instance to execute.</param>
    /// <param name="definition">The job definition containing configuration and metadata.</param>
    /// <param name="cancellationToken">Optional cancellation token for the operation itself.</param>
    /// <returns>A task representing the execution operation.</returns>
    public async Task ExecuteAsync(
        JobInstance instance,
        JobDefinition definition,
        CancellationToken cancellationToken = default)
    {
        if (instance == null)
        {
            throw new ArgumentNullException(nameof(instance));
        }

        if (definition == null)
        {
            throw new ArgumentNullException(nameof(definition));
        }

        logger.LogInformation(
            "Starting execution of job {JobKey}, InstanceId: {InstanceId}",
            instance.JobKey,
            instance.InstanceId);

        CancellationToken jobCancellationToken = default;
        IServiceScope? scope = null;

        try
        {
            // Step 1: Create scoped service provider for job execution
            scope = serviceProvider.CreateScope();

            // Step 2: Get or create distributed cancellation token
            jobCancellationToken = await cancellationManager.GetOrCreateTokenAsync(
                instance.InstanceId,
                cancellationToken);

            logger.LogDebug(
                "Acquired cancellation token for instance {InstanceId}",
                instance.InstanceId);

            // Step 3: Update state to Processing
            await metadataStore.UpdateJobStateAsync(
                instance.InstanceId,
                JobState.Processing,
                null,
                cancellationToken);

            instance.State = JobState.Processing;
            instance.StartedAt = DateTime.UtcNow;

            logger.LogInformation(
                "Job {JobKey} instance {InstanceId} state updated to Processing",
                instance.JobKey,
                instance.InstanceId);

            // Step 4: Resolve and execute task
            var executionTask = ResolveAndExecuteJobAsync(
                scope.ServiceProvider,
                definition,
                instance,
                jobCancellationToken);

            // Step 5: Enforce timeout using Task.WhenAny
            var timeoutTask = Task.Delay(definition.MaxExecutionTimeout, cancellationToken);
            var completedTask = await Task.WhenAny(executionTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                // Timeout occurred
                logger.LogWarning(
                    "Job {JobKey} instance {InstanceId} exceeded timeout of {Timeout}",
                    instance.JobKey,
                    instance.InstanceId,
                    definition.MaxExecutionTimeout);

                // Cancel the distributed token (propagates to all workers if distributed)
                await cancellationManager.CancelTokenAsync(instance.InstanceId, cancellationToken);

                // Wait a brief moment for graceful cancellation
                await Task.WhenAny(executionTask, Task.Delay(TimeSpan.FromSeconds(2)));

                // Update state to Failed
                await UpdateJobStateAsync(
                    instance,
                    JobState.Failed,
                    $"Execution timeout after {definition.MaxExecutionTimeout}",
                    cancellationToken);

                // Check for retry
                await HandleRetryLogicAsync(instance, definition, cancellationToken);
            }
            else
            {
                // Job completed (either successfully or with exception)
                try
                {
                    await executionTask; // Rethrow any exception

                    // Success
                    logger.LogInformation(
                        "Job {JobKey} instance {InstanceId} completed successfully",
                        instance.JobKey,
                        instance.InstanceId);

                    await UpdateJobStateAsync(
                        instance,
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

                    await UpdateJobStateAsync(
                        instance,
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
                        ex.Message);

                    await UpdateJobStateAsync(
                        instance,
                        JobState.Failed,
                        $"{ex.GetType().Name}: {ex.Message}",
                        cancellationToken);

                    // Check for retry
                    await HandleRetryLogicAsync(instance, definition, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            // Catch-all for any unexpected errors in the executor itself
            logger.LogError(
                ex,
                "Critical error in JobExecutor for {JobKey} instance {InstanceId}: {Message}",
                instance.JobKey,
                instance.InstanceId,
                ex.Message);

            try
            {
                await UpdateJobStateAsync(
                    instance,
                    JobState.Failed,
                    $"Executor error: {ex.GetType().Name}: {ex.Message}",
                    cancellationToken);
            }
            catch (Exception updateEx)
            {
                logger.LogError(
                    updateEx,
                    "Failed to update job state after executor error: {Message}",
                    updateEx.Message);
            }
        }
        finally
        {
            // Step 10: Always cleanup cancellation token
            try
            {
                await cancellationManager.DeleteTokenAsync(instance.InstanceId, CancellationToken.None);
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

            logger.LogInformation(
                "Completed execution lifecycle for job {JobKey} instance {InstanceId}",
                instance.JobKey,
                instance.InstanceId);
        }
    }

    /// <summary>
    /// Resolves the job instance from DI and executes it.
    /// </summary>
    private async Task ResolveAndExecuteJobAsync(
        IServiceProvider scopedProvider,
        JobDefinition definition,
        JobInstance instance,
        CancellationToken cancellationToken)
    {
        object? jobInstance = null;

        try
        {
            // Resolve job instance from DI
            jobInstance = scopedProvider.GetRequiredService(definition.JobClrType);

            logger.LogDebug(
                "Resolved job instance of type {JobType} for {JobKey}",
                definition.JobClrType.Name,
                instance.JobKey);
        }
        catch (InvalidOperationException ex)
        {
            // DI resolution failure
            logger.LogError(
                ex,
                "Failed to resolve job type {JobType} from DI container. " +
                "Ensure the job type is registered or has a public constructor with resolvable dependencies.",
                definition.JobClrType.Name);

            throw new InvalidOperationException(
                $"Dependency injection resolution failed for job type {definition.JobClrType.Name}. " +
                $"Error: {ex.Message}",
                ex);
        }

        // Execute based on job type
        if (jobInstance is MoRecurringJob recurringJob)
        {
            // Execute recurring job (no parameters)
            logger.LogDebug(
                "Executing RecurringJob {JobKey} instance {InstanceId}",
                instance.JobKey,
                instance.InstanceId);

            await recurringJob.ExecuteAsync(cancellationToken);
        }
        else
        {
            // Check if it's a TriggeredJob<TParam>
            var jobType = definition.JobClrType;
            var baseType = jobType.BaseType;

            while (baseType != null && !baseType.IsGenericType)
            {
                baseType = baseType.BaseType;
            }

            if (baseType != null &&
                baseType.IsGenericType &&
                baseType.GetGenericTypeDefinition() == typeof(MoTriggeredJob<>))
            {
                // It's a TriggeredJob<TParam>
                var parameterType = baseType.GetGenericArguments()[0];

                logger.LogDebug(
                    "Executing TriggeredJob<{ParamType}> {JobKey} instance {InstanceId}",
                    parameterType.Name,
                    instance.JobKey,
                    instance.InstanceId);

                // Deserialize parameters
                object? parameters = null;
                if (!string.IsNullOrEmpty(instance.Parameters))
                {
                    try
                    {
                        parameters = JsonSerializer.Deserialize(instance.Parameters, parameterType);
                    }
                    catch (JsonException ex)
                    {
                        logger.LogError(
                            ex,
                            "Failed to deserialize parameters for job {JobKey} instance {InstanceId}. " +
                            "Parameters JSON: {Json}",
                            instance.JobKey,
                            instance.InstanceId,
                            instance.Parameters);

                        throw new InvalidOperationException(
                            $"Failed to deserialize job parameters: {ex.Message}",
                            ex);
                    }
                }

                // Call ExecuteAsync via reflection
                var executeMethod = jobType.GetMethod("ExecuteAsync");
                if (executeMethod == null)
                {
                    throw new InvalidOperationException(
                        $"Job type {jobType.Name} does not have ExecuteAsync method");
                }

                var executeTask = executeMethod.Invoke(jobInstance, new[] { parameters, cancellationToken });
                if (executeTask is Task task)
                {
                    await task;
                }
                else
                {
                    throw new InvalidOperationException(
                        $"ExecuteAsync method did not return a Task for {jobType.Name}");
                }
            }
            else
            {
                throw new InvalidOperationException(
                    $"Job type {jobType.Name} does not inherit from RecurringJob or TriggeredJob<TParam>");
            }
        }

        logger.LogDebug(
            "Job execution method completed for {JobKey} instance {InstanceId}",
            instance.JobKey,
            instance.InstanceId);
    }

    /// <summary>
    /// Updates the job instance state in the metadata store.
    /// </summary>
    private async Task UpdateJobStateAsync(
        JobInstance instance,
        JobState newState,
        string? errorMessage,
        CancellationToken cancellationToken)
    {
        instance.State = newState;
        instance.ErrorMessage = errorMessage;

        if (newState == JobState.Succeeded ||
            newState == JobState.Failed ||
            newState == JobState.Terminated ||
            newState == JobState.Cancelled)
        {
            instance.CompletedAt = DateTime.UtcNow;
        }

        await metadataStore.UpdateJobStateAsync(
            instance.InstanceId,
            newState,
            errorMessage,
            cancellationToken);

        logger.LogDebug(
            "Updated job {JobKey} instance {InstanceId} state to {State}",
            instance.JobKey,
            instance.InstanceId,
            newState);
    }

    /// <summary>
    /// Handles retry logic for failed jobs.
    /// </summary>
    private async Task HandleRetryLogicAsync(
        JobInstance instance,
        JobDefinition definition,
        CancellationToken cancellationToken)
    {
        // Check if retries are configured and remaining
        if (definition.RetryCount <= 0)
        {
            logger.LogDebug(
                "No retries configured for job {JobKey}. Marking as Terminated.",
                instance.JobKey);

            await UpdateJobStateAsync(
                instance,
                JobState.Terminated,
                instance.ErrorMessage,
                cancellationToken);

            return;
        }

        if (instance.RetryAttempt >= definition.RetryCount)
        {
            logger.LogWarning(
                "Job {JobKey} instance {InstanceId} exhausted all {RetryCount} retry attempts. Marking as Terminated.",
                instance.JobKey,
                instance.InstanceId,
                definition.RetryCount);

            await UpdateJobStateAsync(
                instance,
                JobState.Terminated,
                instance.ErrorMessage,
                cancellationToken);

            return;
        }

        // Increment retry attempt
        instance.RetryAttempt++;

        logger.LogInformation(
            "Retrying job {JobKey} instance {InstanceId}. Attempt {Attempt}/{Max}",
            instance.JobKey,
            instance.InstanceId,
            instance.RetryAttempt,
            definition.RetryCount);

        // Update retry attempt in metadata store
        await metadataStore.SaveJobInstanceAsync(instance, cancellationToken);

        // Re-publish job execution event for retry
        try
        {
            var retryEvent = new MoJobExecutionEvent
            {
                InstanceId = instance.InstanceId,
                JobKey = instance.JobKey,
                Parameters = instance.Parameters,
                RequestedAt = DateTime.UtcNow
            };

            await eventBus.PublishAsync(retryEvent);

            logger.LogInformation(
                "Published retry event for job {JobKey} instance {InstanceId}",
                instance.JobKey,
                instance.InstanceId);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to publish retry event for job {JobKey} instance {InstanceId}: {Message}",
                instance.JobKey,
                instance.InstanceId,
                ex.Message);

            // Mark as terminated if we can't retry
            await UpdateJobStateAsync(
                instance,
                JobState.Terminated,
                $"Failed to publish retry event: {ex.Message}",
                cancellationToken);
        }
    }
}
