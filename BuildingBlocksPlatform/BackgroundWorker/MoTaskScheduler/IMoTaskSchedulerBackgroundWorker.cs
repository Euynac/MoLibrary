using BuildingBlocksPlatform.BackgroundWorker.Abstract.Workers;

namespace BuildingBlocksPlatform.BackgroundWorker.MoTaskScheduler;

public interface IMoTaskSchedulerBackgroundWorker : IMoSimpleBackgroundWorker
{
    string CronExpression { get; set; }

    Task DoWorkAsync(CancellationToken cancellationToken = default);
}