using MoLibrary.BackgroundJob.Abstract.Workers;

namespace MoLibrary.BackgroundJob.MoTaskScheduler;

public interface IMoTaskSchedulerBackgroundWorker : IMoSimpleBackgroundWorker
{
    string CronExpression { get; set; }

    Task DoWorkAsync(CancellationToken cancellationToken = default);
}