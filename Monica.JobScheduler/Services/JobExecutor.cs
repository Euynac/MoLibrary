using Microsoft.Extensions.Logging;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.Exceptions;
using Monica.JobScheduler.Models;

namespace Monica.JobScheduler.Services;

/// <summary>
/// Executes jobs for both recurring and triggered job types.
/// Handles the actual job invocation logic while the orchestrator manages state, timeout, and retries.
/// </summary>
public class JobExecutor(
    JobInstanceManager jobInstanceManager,
    ILogger<JobExecutor> logger)
{
    public async Task ExecuteRecurringJobAsync(JobExecutionContext context)
    {
        var job = context.ServiceProvider.GetService(context.JobType);
        if (job == null)
        {
            throw new InvalidOperationException(
                $"The job type is not registered in DI: {context.JobType.FullName}");
        }

        if (job is not IRecurringJob recurringJob)
        {
            throw new InvalidOperationException(
                $"Job type does not implement {nameof(IRecurringJob)}: {context.JobType.FullName}");
        }

        BindExecutionLogWriter(job, context.InstanceId);
        try
        {
            await recurringJob.ExecuteAsync(context.CancellationToken);
        }
        finally
        {
            ClearExecutionLogWriter(job);
        }
    }

    public async Task ExecuteTriggeredJobAsync(JobExecutionContext context)
    {
        var job = context.ServiceProvider.GetService(context.JobType);
        if (job == null)
        {
            throw new InvalidOperationException(
                $"The job type is not registered in DI: {context.JobType.FullName}");
        }

        // Find ExecuteAsync method via reflection
        var jobExecuteMethod = context.JobType.GetMethod(nameof(ITriggeredJob<object>.ExecuteAsync));
        if (jobExecuteMethod == null)
        {
            throw new InvalidOperationException(
                $"Job type does not implement {typeof(ITriggeredJob<>).Name}: {context.JobType.FullName}");
        }

        BindExecutionLogWriter(job, context.InstanceId);
        try
        {
            // Invoke ExecuteAsync with args and cancellation token
            await (Task)jobExecuteMethod.Invoke(job, [context.JobArgs, context.CancellationToken])!;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Triggered job execution failed for job type: {JobType}",
                context.JobType.FullName);

            throw new TriggeredJobExecutionException(
                "Triggered job execution failed. See inner exception for details.", ex)
            {
                JobType = context.JobType.AssemblyQualifiedName!,
                JobArgs = context.JobArgs
            };
        }
        finally
        {
            ClearExecutionLogWriter(job);
        }
    }

    private void BindExecutionLogWriter(object job, string instanceId)
    {
        if (job is not IJobExecutionLogBindingTarget target)
        {
            return;
        }

        target.BindExecutionLogWriter(new JobExecutionLogWriter(
            instanceId,
            (message, logLevel, exception, cancellationToken) =>
                jobInstanceManager.AppendExecutionLogAsync(
                    instanceId,
                    message,
                    logLevel,
                    exception,
                    cancellationToken)));
    }

    private static void ClearExecutionLogWriter(object job)
    {
        if (job is IJobExecutionLogBindingTarget target)
        {
            target.ClearExecutionLogWriter();
        }
    }
}
