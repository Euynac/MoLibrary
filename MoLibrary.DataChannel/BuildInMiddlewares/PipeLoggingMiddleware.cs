using MoLibrary.DataChannel.Pipeline;

namespace MoLibrary.DataChannel.BuildInMiddlewares;

public class PipeLoggingMiddleware : PipeMonitorMiddlewareBase
{
    public override DataContext Pass(DataContext context)
    {
        return base.Pass(context);
    }
}