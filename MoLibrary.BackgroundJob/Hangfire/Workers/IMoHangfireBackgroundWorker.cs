using MoLibrary.BackgroundJob.Abstract.Workers;

namespace MoLibrary.BackgroundJob.Hangfire.Workers;

public interface IMoHangfireBackgroundWorker : IMoDashboardBackgroundWorker
{
    string CronExpression { get; set; }

    string Queue { get; set; }
    string? RecurringJobId { get; set; }

    TimeZoneInfo? TimeZone { get; set; }

    Task InternalDoWorkAsync(CancellationToken cancellationToken = default);
}