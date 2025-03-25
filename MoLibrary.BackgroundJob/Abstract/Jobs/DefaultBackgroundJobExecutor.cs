using Microsoft.Extensions.Logging;

namespace MoLibrary.BackgroundJob.Abstract.Jobs;

public class DefaultBackgroundJobExecutor(ILogger<DefaultBackgroundJobExecutor> logger) : IBackgroundJobExecutor
{
    public ILogger<DefaultBackgroundJobExecutor> Logger { protected get; set; } = logger;

    public virtual async Task ExecuteAsync(JobExecutionContext context)
    {
        var job = context.ServiceProvider.GetService(context.JobType);
        if (job == null)
        {
            throw new Exception("The job type is not registered to DI: " + context.JobType);
        }

        var jobExecuteMethod = 
                               context.JobType.GetMethod(nameof(IMoBackgroundJob<object>.ExecuteAsync));
        if (jobExecuteMethod == null)
        {
            throw new Exception(
                $"Given job type does not implement {typeof(IMoBackgroundJob<>).Name}. " +
                "The job type was: " + context.JobType);
        }

        try
        {
            if (jobExecuteMethod.Name == nameof(IMoBackgroundJob<object>.ExecuteAsync))
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
            Logger.LogError("A background job execution is failed. See inner exception for details.{1}", ex);

            throw new BackgroundJobExecutionException(
                "A background job execution is failed. See inner exception for details.", ex)
            {
                JobType = context.JobType.AssemblyQualifiedName!,
                JobArgs = context.JobArgs
            };
        }
    }
}