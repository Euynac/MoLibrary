using Microsoft.Extensions.Logging;

namespace MoLibrary.Core.Features.MoTimekeeper;

public class AutoTimekeeper : MoTimekeeperBase
{
    // ReSharper disable once VirtualMemberCallInConstructor
    public AutoTimekeeper(string key, ILogger logger) : base(key, logger) => Start();

    public override void Dispose()
    {
        if (Disposed) return;
        Disposed = true;
        Finish();
        LoggingElapsedMs();
    }
}