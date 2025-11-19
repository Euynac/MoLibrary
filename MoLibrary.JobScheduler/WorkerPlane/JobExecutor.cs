using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoLibrary.JobScheduler.Abstractions;
using MoLibrary.JobScheduler.Exceptions;
using MoLibrary.JobScheduler.Models;

namespace MoLibrary.JobScheduler.WorkerPlane;

/// <summary>
/// Executes jobs for both recurring and triggered job types.
/// Handles the actual job invocation logic while the orchestrator manages state, timeout, and retries.
/// </summary>
public class JobExecutor(ILogger<JobExecutor> logger)
{
    public async Task ExecuteRecurringJobAsync(JobExecutionContext context)
    {
        var job = context.ServiceProvider.GetService(context.JobType);
        if (job == null)
        {
            throw new InvalidOperationException(
                $"The job type is not registered in DI: {context.JobType.FullName}");
        }

        if (job is not IMoRecurringJob recurringJob)
        {
            throw new InvalidOperationException(
                $"Job type does not implement {nameof(IMoRecurringJob)}: {context.JobType.FullName}");
        }

        await recurringJob.ExecuteAsync(context.CancellationToken);
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
        var jobExecuteMethod = context.JobType.GetMethod(nameof(IMoTriggeredJob<object>.ExecuteAsync));
        if (jobExecuteMethod == null)
        {
            throw new InvalidOperationException(
                $"Job type does not implement {typeof(IMoTriggeredJob<>).Name}: {context.JobType.FullName}");
        }

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
    }
}