namespace BuildingBlocksPlatform.BackgroundWorker.Hangfire.Workers;

public abstract class MoHangfireBackgroundWorker : IMoHangfireBackgroundWorker
{
    public string? RecurringJobId { get; set; }

    public string CronExpression { get; set; } 

    public TimeZoneInfo? TimeZone { get; set; } = null;

    public string Queue { get; set; } = "default";

    public abstract Task DoWorkAsync(CancellationToken cancellationToken = default);
    internal MoHangfireBackgroundWorker(string cronExpression)
    {
        RecurringJobId = GetType().FullName;
        CronExpression = cronExpression;
    }
    public override string ToString()
    {
        return GetType().FullName!;
    }
}