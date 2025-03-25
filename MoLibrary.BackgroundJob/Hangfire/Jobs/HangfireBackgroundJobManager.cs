using System.Collections.Concurrent;
using Hangfire.Redis;
using Hangfire.States;
using MoLibrary.BackgroundJob.Abstract.Jobs;

namespace MoLibrary.BackgroundJob.Hangfire.Jobs;
public class HangfireBackgroundJobManager : IMoBackgroundJobManager
{
    public static readonly ConcurrentDictionary<Type, Type> JobTypeMap = new();

    public virtual Task<string> EnqueueAsync<TArgs>(TArgs args,
        BackgroundJobPriority priority = BackgroundJobPriority.Normal,
        TimeSpan? delay = null)
    {
        return Task.FromResult(delay.HasValue
            ? global::Hangfire.BackgroundJob.Schedule<HangfireJobExecutionAdapter<TArgs>>(
                adapter => adapter.ExecuteAsync(GetQueueName(typeof(TArgs)), args, CancellationToken.None),
                delay.Value
            )
            : global::Hangfire.BackgroundJob.Enqueue<HangfireJobExecutionAdapter<TArgs>>(
                adapter => adapter.ExecuteAsync(GetQueueName(typeof(TArgs)), args, CancellationToken.None)
            ));
    }

    public Task RegisterJob<TJobType, TArgs>() where TJobType : IMoBackgroundJob<TArgs>
    {
        JobTypeMap.AddOrUpdate(typeof(TArgs), typeof(TJobType), (_, _) => typeof(TJobType));
        return Task.CompletedTask;
    }

    protected virtual string GetQueueName(Type argsType)
    {
        return HangfireRedisGlobalOptions.Queues.FirstOrDefault() ?? EnqueuedState.DefaultQueue;
    }
}
