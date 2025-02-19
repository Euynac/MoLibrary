using BuildingBlocksPlatform.DependencyInjection.AppInterfaces;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocksPlatform.BackgroundWorker.Abstract.Jobs;

public abstract class AsyncBackgroundJob<TArgs>(IMoServiceProvider serviceProvider) : IAsyncBackgroundJob<TArgs>
{
    public ILogger<AsyncBackgroundJob<TArgs>> Logger { get; } =
        serviceProvider.ServiceProvider.GetRequiredService<ILogger<AsyncBackgroundJob<TArgs>>>();

    public abstract Task ExecuteAsync(TArgs args);
}