using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace MoLibrary.Core.Features.MoTimekeeper;

public class MoTimekeeperFactory(IHttpContextAccessor accessor, ILogger<MoTimekeeperFactory> logger) : IMoTimekeeperFactory
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