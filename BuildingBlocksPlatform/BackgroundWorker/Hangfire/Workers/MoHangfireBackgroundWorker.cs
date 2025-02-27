namespace BuildingBlocksPlatform.BackgroundWorker.Hangfire.Workers;

public abstract class MoHangfireBackgroundWorker : IMoHangfireBackgroundWorker
{
    public string? RecurringJobId { get; set; }

    public string CronExpression { get; set; } 

    public TimeZoneInfo? TimeZone { get; set; } = null;

    public string Queue { get; set; } = "default";

    public virtual async Task InternalDoWorkAsync(CancellationToken cancellationToken = default)
    {
        await DoWorkAsync(cancellationToken);
    }

    public abstract Task DoWorkAsync(CancellationToken cancellationToken = default);
    internal MoHangfireBackgroundWorker(string cronExpression, IMoServiceProvider provider)
    {
        RecurringJobId = GetType().FullName;
        CronExpression = cronExpression;
        ServiceProvider = provider.ServiceProvider;
    }

    protected IServiceProvider ServiceProvider { get; init; }
    public override string ToString()
    {
        return GetType().FullName!;
    }
}