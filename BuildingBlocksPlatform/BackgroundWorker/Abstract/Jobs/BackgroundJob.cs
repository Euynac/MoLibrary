using Microsoft.Extensions.Logging.Abstractions;

namespace BuildingBlocksPlatform.BackgroundWorker.Abstract.Jobs;

public abstract class BackgroundJob<TArgs> : IBackgroundJob<TArgs>
{
    protected BackgroundJob()
    {
        Logger = NullLogger<BackgroundJob<TArgs>>.Instance;
    }
    //TODO: Add UOW, Localization, CancellationTokenProvider and other useful properties..?

    public ILogger<BackgroundJob<TArgs>> Logger { get; set; }

    public abstract void Execute(TArgs args);
}