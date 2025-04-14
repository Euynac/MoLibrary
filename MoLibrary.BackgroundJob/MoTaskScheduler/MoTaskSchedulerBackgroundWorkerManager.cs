using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoLibrary.BackgroundJob.Abstract.Workers;
using MoLibrary.DependencyInjection.AppInterfaces;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.BackgroundJob.MoTaskScheduler;

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
            var worker = (IMoTaskSchedulerBackgroundWorker)ServiceProvider.GetRequiredService(workerType);
            scheduler.AddTask(worker.CronExpression, () =>
            {
                var factory = ServiceProvider.GetRequiredService<IServiceScopeFactory>();
                using var scope = factory.CreateScope();
                var scopedProvider = scope.ServiceProvider;
                ((IMoTaskSchedulerBackgroundWorker)scopedProvider.GetRequiredService(workerType)).DoWorkAsync(
                    cancellationToken);
            });
        }
        else
        {
            logger.LogError($"添加Worker失败：{workerType.FullName}，未实现{nameof(IMoTaskSchedulerBackgroundWorker)}");
        }
    }
}