namespace BuildingBlocksPlatform.BackgroundWorker.Abstract.Jobs;

public interface IBackgroundJobExecutor
{
    Task ExecuteAsync(JobExecutionContext context);
}