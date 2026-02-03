using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Monica.Core.Features.MoTimekeeper;

public class MoTimekeeperFactory(ILogger<MoTimekeeperFactory> logger) : IMoTimekeeperFactory
{
    public AutoTimekeeper CreateAutoTimer(string key, string? content = null)
    {
        var keeper = new AutoTimekeeper(key, logger)
        {
            Content = content
        };
        return keeper;
    }

    public NormalTimekeeper CreateNormalTimer(string key)
    {
        return new NormalTimekeeper(key, logger);
    }
}