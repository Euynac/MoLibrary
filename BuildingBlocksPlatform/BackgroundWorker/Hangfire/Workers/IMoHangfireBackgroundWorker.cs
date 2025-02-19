using BuildingBlocksPlatform.BackgroundWorker.Abstract.Workers;

namespace BuildingBlocksPlatform.BackgroundWorker.Hangfire.Workers;

public interface IMoHangfireBackgroundWorker : IMoDashboardBackgroundWorker
{
    string CronExpression { get; set; }

    string Queue { get; set; }
    string? RecurringJobId { get; set; }

    TimeZoneInfo? TimeZone { get; set; }

    Task DoWorkAsync(CancellationToken cancellationToken = default);
}