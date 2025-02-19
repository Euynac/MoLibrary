using BuildingBlocksPlatform.BackgroundWorker.Abstract.Jobs;
using BuildingBlocksPlatform.Core.Model;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocksPlatform.BackgroundWorker.Hangfire.Jobs;

public class HangfireJobExecutionAdapter<TArgs>(
    IBackgroundJobExecutor executor,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<HangfireJobExecutionAdapter<TArgs>> logger)
{
    [Queue("{0}")]
    public async Task ExecuteAsync(string queue, TArgs args, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        if (UnitBackgroundJob.ArgsToJobDict.TryGetValue(typeof(TArgs), out var job))
        {
            var context = new JobExecutionContext(scope.ServiceProvider, job.Type, args!,
                cancellationToken: cancellationToken);
            await executor.ExecuteAsync(context);
        }
        else
        {
            logger.LogError("找不到相应{name}的Job Handler，作业执行失败", typeof(TArgs).Name);
        }
    }
}