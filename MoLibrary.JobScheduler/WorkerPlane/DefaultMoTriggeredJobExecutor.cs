using Microsoft.Extensions.Logging;
using MoLibrary.JobScheduler.Abstractions;
using MoLibrary.JobScheduler.Exceptions;
using MoLibrary.JobScheduler.Models;

namespace MoLibrary.JobScheduler.WorkerPlane;

public class DefaultMoTriggeredJobExecutor(ILogger<DefaultMoTriggeredJobExecutor> logger) : IMoTriggeredJobExecutor
{
    public virtual async Task ExecuteAsync(JobExecutionContext context)
    {
        var job = context.ServiceProvider.GetService(context.JobType);
        if (job == null)
        {
            throw new Exception("The job type is not registered to DI: " + context.JobType);
        }

        var jobExecuteMethod = 
                               context.JobType.GetMethod(nameof(IMoTriggeredJob<object>.ExecuteAsync));
        if (jobExecuteMethod == null)
        {
            throw new Exception(
                $"Given job type does not implement {typeof(IMoTriggeredJob<>).Name}. " +
                "The job type was: " + context.JobType);
        }

        try
        {
            if (jobExecuteMethod.Name == nameof(IMoTriggeredJob<object>.ExecuteAsync))
            {
                await (Task) jobExecuteMethod.Invoke(job, [context.JobArgs])!;
            }
            else
            {
                jobExecuteMethod.Invoke(job, [context.JobArgs]);
            }
        }
        catch (Exception ex)
        {
            logger.LogError("A background job execution is failed. See inner exception for details.{ex}", ex);

            throw new TriggeredJobExecutionException(
                "A background job execution is failed. See inner exception for details.", ex)
            {
                JobType = context.JobType.AssemblyQualifiedName!,
                JobArgs = context.JobArgs
            };
        }
    }
}