using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoLibrary.BackgroundJob.Abstract.Workers;
using MoLibrary.Core.Features.MoTimekeeper;
using MoLibrary.DependencyInjection.AppInterfaces;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.BackgroundJob.MoTaskScheduler;

/// <summary>
/// 基于MoTaskScheduler的后台工作管理器
/// </summary>
public class MoTaskSchedulerBackgroundWorkerManager(
    IMoServiceProvider serviceProvider,
    ILogger<MoTaskSchedulerBackgroundWorkerManager> logger,
    IMoTaskScheduler scheduler, IOptions<ModuleOptionBackgroundJob> options)
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

            var moduleOption = options.Value;

            var timekeeper = moduleOption.EnableWorkerDurationMonitor ? ServiceProvider.GetRequiredService<IMoTimekeeper>() : null;
            async void Action()
            {
                try
                {
                    var keeper = timekeeper?.CreateNormalTimer(workerType.GetCleanFullName());
                    keeper?.Start();
                    // 使用Task.Run创建新线程运行任务，避免阻塞调度器线程
                    await Task.Run(async () =>
                    {
                        try
                        {
                            var factory = ServiceProvider.GetRequiredService<IServiceScopeFactory>();
                            await using var scope = factory.CreateAsyncScope();
                            var scopedProvider = scope.ServiceProvider;
                            await ((IMoTaskSchedulerBackgroundWorker) scopedProvider.GetRequiredService(workerType)).DoWorkAsync(cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, $"执行后台任务 {workerType.Name} 时发生错误");
                        }
                    }, cancellationToken);
                    keeper?.Finish();
                }
                catch (Exception e)
                {
                    logger.LogError(e, $"执行后台任务 {workerType.Name} 时发生错误");
                }
            }

            scheduler.AddTask(worker.CronExpression, Action);
        }
        else
        {
            logger.LogError($"添加Worker失败：{workerType.FullName}，未实现{nameof(IMoTaskSchedulerBackgroundWorker)}");
        }
    }
}