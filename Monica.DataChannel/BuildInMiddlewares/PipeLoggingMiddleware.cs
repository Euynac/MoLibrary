using Monica.DataChannel.Pipeline;

namespace Monica.DataChannel.BuildInMiddlewares;

public class PipeLoggingMiddleware : PipeMonitorMiddlewareBase
{
    public override DataContext Pass(DataContext context)
    {
        return base.Pass(context);
    }
}