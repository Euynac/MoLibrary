using BuildingBlocksPlatform.BackgroundWorker.Abstract.Jobs;
using Hangfire;
using Hangfire.Redis;
using Hangfire.States;

namespace BuildingBlocksPlatform.BackgroundWorker.Hangfire.Jobs;

public class HangfireBackgroundJobManager : IBackgroundJobManager
{
    public virtual Task<string> EnqueueAsync<TArgs>(TArgs args,
        BackgroundJobPriority priority = BackgroundJobPriority.Normal,
        TimeSpan? delay = null)
    {
        return Task.FromResult(delay.HasValue
            ? BackgroundJob.Schedule<HangfireJobExecutionAdapter<TArgs>>(
                adapter => adapter.ExecuteAsync(GetQueueName(typeof(TArgs)), args, default),
                delay.Value
            )
            : BackgroundJob.Enqueue<HangfireJobExecutionAdapter<TArgs>>(
                adapter => adapter.ExecuteAsync(GetQueueName(typeof(TArgs)), args, default)
            ));
    }

    protected virtual string GetQueueName(Type argsType)
    {
        return HangfireRedisGlobalOptions.Queues.FirstOrDefault() ?? EnqueuedState.DefaultQueue;
    }
}