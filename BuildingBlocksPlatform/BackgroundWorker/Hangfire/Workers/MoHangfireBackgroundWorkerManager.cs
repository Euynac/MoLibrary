using BuildingBlocksPlatform.BackgroundWorker.Abstract.Workers;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocksPlatform.BackgroundWorker.Hangfire.Workers;

public class MoHangfireBackgroundWorkerManager(
    IMoServiceProvider serviceProvider,
    ILogger<MoHangfireBackgroundWorkerManager> logger)
    : IMoDashboardBackgroundWorkerManager, IDisposable
{
    protected IServiceProvider ServiceProvider { get; } = serviceProvider.ServiceProvider;


    public void Dispose()
    {
    }

    public Task TriggerDashboardJobOnce(Type workerType)
    {
        if (workerType.IsImplementInterface(typeof(IMoHangfireBackgroundWorker)))
        {
            var hangfireBackgroundWorker = (IMoHangfireBackgroundWorker)ServiceProvider.GetRequiredService(workerType);

            var jobName = string.IsNullOrWhiteSpace(hangfireBackgroundWorker.RecurringJobId)
                ? workerType.FullName
                : hangfireBackgroundWorker.RecurringJobId;

            RecurringJob.TriggerJob(jobName);
        }
        else
        {
            logger.LogError($"添加Worker失败：{workerType.FullName}，未实现{nameof(IMoHangfireBackgroundWorker)}");
        }

        return Task.CompletedTask;
    }

    public Task AddToDashboardAsync(Type workerType, string? queue = null,
        CancellationToken cancellationToken = default)
    {
        if (workerType.IsImplementInterface(typeof(IMoHangfireBackgroundWorker)))
        {
            var hangfireBackgroundWorker = (IMoHangfireBackgroundWorker) ServiceProvider.GetRequiredService(workerType);

            var jobName = string.IsNullOrWhiteSpace(hangfireBackgroundWorker.RecurringJobId)
                ? workerType.FullName
                : hangfireBackgroundWorker.RecurringJobId;

            RecurringJob.AddOrUpdate(jobName,
                () => ((IMoHangfireBackgroundWorker) ServiceProvider.GetRequiredService(workerType)).InternalDoWorkAsync(
                    cancellationToken),
                hangfireBackgroundWorker.CronExpression, hangfireBackgroundWorker.TimeZone,
                queue ?? hangfireBackgroundWorker.Queue);
        }
        else
        {
            logger.LogError($"添加Worker失败：{workerType.FullName}，未实现{nameof(IMoHangfireBackgroundWorker)}");
        }

        return Task.CompletedTask;
    }
}