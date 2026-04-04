using Monica.DataChannel.Pipeline;

namespace Monica.DataChannel.Middlewares;

public class PipelineLoggingMiddleware : PipelineMonitorMiddlewareBase
{
    public override ChannelDataContext Pass(ChannelDataContext context)
    {
        return base.Pass(context);
    }
}