using Hangfire.Redis;
using Hangfire.States;
using MoLibrary.BackgroundJob.Abstract.Jobs;

namespace MoLibrary.BackgroundJob.Hangfire.Jobs;
public class HangfireBackgroundJobManager : IMoBackgroundJobManager
{
    public virtual Task<string> EnqueueAsync<TArgs>(TArgs args,
        BackgroundJobPriority priority = BackgroundJobPriority.Normal,
        TimeSpan? delay = null)
    {
        var result = delay.HasValue
            ? global::Hangfire.BackgroundJob.Schedule<HangfireJobExecutionAdapter<TArgs>>(
                adapter => adapter.ExecuteAsync(GetQueueName(typeof(TArgs)), args, CancellationToken.None),
                delay.Value
            )
            : global::Hangfire.BackgroundJob.Enqueue<HangfireJobExecutionAdapter<TArgs>>(
                adapter => adapter.ExecuteAsync(GetQueueName(typeof(TArgs)), args, CancellationToken.None)
            );
        return Task.FromResult(result);
    }

    protected virtual string GetQueueName(Type argsType)
    {
        return HangfireRedisGlobalOptions.Queues.FirstOrDefault() ?? EnqueuedState.DefaultQueue;
    }
}
