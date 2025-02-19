using BuildingBlocksPlatform.BackgroundWorker.Abstract.Jobs;
using BuildingBlocksPlatform.BackgroundWorker.Abstract.Workers;
using BuildingBlocksPlatform.BackgroundWorker.Hangfire.Jobs;
using BuildingBlocksPlatform.BackgroundWorker.Hangfire.Workers;
using BuildingBlocksPlatform.BackgroundWorker.MoTaskScheduler;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocksPlatform.BackgroundWorker;

public static class ServiceCollectionExtension
{
    public static void AddMoBackgroundWorker(this IServiceCollection services)
    {
        services.AddSingleton<IMoDashboardBackgroundWorkerManager, MoHangfireBackgroundWorkerManager>();
        services.AddSingleton<IMoSimpleBackgroundWorkerManager, MoTaskSchedulerBackgroundWorkerManager>();
        services.AddSingleton<IMoBackgroundWorkerManager, MoBackgroundWorkerManager>();
        services.AddSingleton<IMoTaskScheduler, MoTaskSchedulerInMemoryProvider>();


        services.AddTransient<IBackgroundJobManager, HangfireBackgroundJobManager>();
        services.AddTransient<IBackgroundJobExecutor, BackgroundJobExecutor>();
    }
}