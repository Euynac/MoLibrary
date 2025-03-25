namespace MoLibrary.BackgroundJob.Abstract.Jobs;

public interface IBackgroundJobExecutor
{
    Task ExecuteAsync(JobExecutionContext context);
}