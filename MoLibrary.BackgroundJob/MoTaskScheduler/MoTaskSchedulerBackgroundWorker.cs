namespace MoLibrary.BackgroundJob.MoTaskScheduler;

public abstract class MoTaskSchedulerBackgroundWorker(string cronExpression) : IMoTaskSchedulerBackgroundWorker
{
    public TimeZoneInfo? TimeZone { get; set; } = null;
    public string CronExpression { get; set; } = cronExpression;

    public abstract Task DoWorkAsync(CancellationToken cancellationToken = default);

    public override string ToString()
    {
        return GetType().FullName!;
    }
}