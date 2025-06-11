using Microsoft.Extensions.Logging;

namespace MoLibrary.Core.Features.MoTimekeeper;

public class AutoTimekeeper : MoTimekeeperBase
{
    public AutoTimekeeper(string key, ILogger logger) : base(key, logger) => Timer.Start();
    public override void Dispose()
    {
        if (Disposed) return;
        Disposed = true;
        Finish();
        LoggingElapsedMs();
    }
}