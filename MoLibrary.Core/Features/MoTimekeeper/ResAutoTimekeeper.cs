using System.Dynamic;
using Microsoft.Extensions.Logging;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.Core.Features.MoTimekeeper;

public class ResAutoTimekeeper : MoTimekeeperBase
{
    private readonly MoRequestContext? _context;

    public ResAutoTimekeeper(MoRequestContext context, string key, ILogger logger) : base(key, logger)
    {
        _context = context;
        Start();
    }

    public override void Dispose()
    {
        if (Disposed) return;
        Disposed = true;
        Finish();
        if (_context is not null)
        {
            _context.OtherInfo ??= new ExpandoObject();
            _context.OtherInfo.Append("timer", new { name = Key, duration = $"{Timer.ElapsedMilliseconds}ms" });
        }
        LoggingElapsedMs();
    }
}