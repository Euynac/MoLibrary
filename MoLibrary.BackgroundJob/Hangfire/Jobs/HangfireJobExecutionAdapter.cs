using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoLibrary.BackgroundJob.Abstract.Jobs;

namespace MoLibrary.BackgroundJob.Hangfire.Jobs;

public class HangfireJobExecutionAdapter<TArgs>(
    IBackgroundJobExecutor executor,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<HangfireJobExecutionAdapter<TArgs>> logger)
{
    [Queue("{0}")]
    public async Task ExecuteAsync(string queue, TArgs args, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        if (IMoBackgroundJobManager.JobTypeMap.TryGetValue(typeof(TArgs), out var job))
        {
            var context = new JobExecutionContext(scope.ServiceProvider, job, args!,
                cancellationToken: cancellationToken);
            await executor.ExecuteAsync(context);
        }
        else
        {
            logger.LogError("找不到相应{name}的Job Handler，作业执行失败", typeof(TArgs).Name);
        }
    }
}