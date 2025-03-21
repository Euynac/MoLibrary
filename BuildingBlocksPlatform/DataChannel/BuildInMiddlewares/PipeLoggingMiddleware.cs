using BuildingBlocksPlatform.DataChannel.Pipeline;

namespace BuildingBlocksPlatform.DataChannel.BuildInMiddlewares;

public class PipeLoggingMiddleware : PipeMonitorMiddlewareBase
{
    public override DataContext Pass(DataContext context)
    {
        return base.Pass(context);
    }
}