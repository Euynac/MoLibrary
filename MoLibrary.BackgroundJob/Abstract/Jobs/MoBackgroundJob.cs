using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoLibrary.DependencyInjection.AppInterfaces;

namespace MoLibrary.BackgroundJob.Abstract.Jobs;

public abstract class MoBackgroundJob<TArgs>(IMoServiceProvider serviceProvider) : IMoBackgroundJob<TArgs>
{
    public ILogger<MoBackgroundJob<TArgs>> Logger { get; } =
        serviceProvider.ServiceProvider.GetRequiredService<ILogger<MoBackgroundJob<TArgs>>>();

    public abstract Task ExecuteAsync(TArgs args);
}