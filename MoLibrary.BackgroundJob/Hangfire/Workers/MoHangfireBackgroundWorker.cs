using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MoLibrary.BackgroundJob.Modules;
using MoLibrary.Core.Features.MoTimekeeper;
using MoLibrary.DependencyInjection.AppInterfaces;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.BackgroundJob.Hangfire.Workers;

public abstract class MoHangfireBackgroundWorker : IMoHangfireBackgroundWorker
{
    public string? RecurringJobId { get; set; }

    public string CronExpression { get; set; } 

    public TimeZoneInfo? TimeZone { get; set; } = null;

    public string Queue { get; set; } = "default";

    public virtual async Task InternalDoWorkAsync(CancellationToken cancellationToken = default)
    {
        var option = ServiceProvider.GetRequiredService<IOptions<ModuleBackgroundJobOption>>();
        var factory = option.Value.EnableWorkerDurationMonitor
            ? ServiceProvider.GetRequiredService<IMoTimekeeperFactory>()
            : null;
        using var keeper = factory?.CreateNormalTimer(GetType().GetCleanFullName());
        keeper?.Start();
        await DoWorkAsync(cancellationToken);
        keeper?.Finish();
    }
    public abstract Task DoWorkAsync(CancellationToken cancellationToken = default);

    protected MoHangfireBackgroundWorker(string cronExpression, IMoServiceProvider provider)
    {
        RecurringJobId = GetType().FullName;
        CronExpression = cronExpression;
        ServiceProvider = provider.ServiceProvider;
    }

    protected IServiceProvider ServiceProvider { get; init; }
    public override string ToString()
    {
        return GetType().FullName!;
    }
}