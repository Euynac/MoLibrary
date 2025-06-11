using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MoLibrary.Core.Extensions;
using MoLibrary.Tool.Utils;

namespace MoLibrary.Core.Features.MoTimekeeper;

public class MoTimekeeperManager(IHttpContextAccessor accessor, ILogger<MoTimekeeperManager> logger) : IMoTimekeeper
{
    public IDisposable CreateResAutoTimer(string key)
    {
        if (accessor.HttpContext?.GetOrNew<MoRequestContext>() is { } context)
        {
            return new ResAutoTimekeeper(context, key, logger);
        }

        return NullDisposable.Instance;
    }

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