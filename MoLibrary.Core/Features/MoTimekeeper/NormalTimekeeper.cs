using Microsoft.Extensions.Logging;

namespace MoLibrary.Core.Features.MoTimekeeper;

public class NormalTimekeeper(string key, ILogger logger) : MoTimekeeperBase(key, logger)
{
    public override void Finish()
    {
        base.Finish();
        LoggingElapsedMs();
    }
}