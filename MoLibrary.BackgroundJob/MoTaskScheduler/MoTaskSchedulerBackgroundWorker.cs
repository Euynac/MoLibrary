namespace MoLibrary.BackgroundJob.MoTaskScheduler;

public abstract class MoTaskSchedulerBackgroundWorker : IMoTaskSchedulerBackgroundWorker
{
    public TimeZoneInfo? TimeZone { get; set; } = null;
    public string CronExpression { get; set; } 

    public abstract Task DoWorkAsync(CancellationToken cancellationToken = default);
    public MoTaskSchedulerBackgroundWorker(string cronExpression)
    {
        CronExpression = cronExpression;
    }
    public override string ToString()
    {
        return GetType().FullName!;
    }
}