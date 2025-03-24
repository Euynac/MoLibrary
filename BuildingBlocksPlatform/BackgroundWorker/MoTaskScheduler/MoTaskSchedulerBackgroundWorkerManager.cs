using BuildingBlocksPlatform.BackgroundWorker.Abstract.Workers;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.DependencyInjection.AppInterfaces;

namespace BuildingBlocksPlatform.BackgroundWorker.MoTaskScheduler;

public class MoTaskSchedulerBackgroundWorkerManager(
    IMoServiceProvider serviceProvider,
    ILogger<MoTaskSchedulerBackgroundWorkerManager> logger,
    IMoTaskScheduler scheduler)
    : IMoSimpleBackgroundWorkerManager, IDisposable
{
    protected IServiceProvider ServiceProvider { get; } = serviceProvider.ServiceProvider;


    public void Dispose()
    {
    }


    public async Task AddAsync(Type workerType, CancellationToken cancellationToken = default)
    {
        if (workerType.IsImplementInterface(typeof(IMoTaskSchedulerBackgroundWorker)))
        {
            var worker = (IMoTaskSchedulerBackgroundWorker) ServiceProvider.GetRequiredService(workerType);
            scheduler.AddTask(worker.CronExpression, () =>
            {
                ((IMoTaskSchedulerBackgroundWorker) ServiceProvider.GetRequiredService(workerType)).DoWorkAsync(
                    cancellationToken);
            });
        }
        else
        {
            logger.LogError($"添加Worker失败：{workerType.FullName}，未实现{nameof(IMoTaskSchedulerBackgroundWorker)}");
        }
    }
}