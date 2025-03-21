using BuildingBlocksPlatform.DataChannel.Pipeline;

namespace BuildingBlocksPlatform.DataChannel.BuildInMiddlewares;

/// <summary>
/// 转换中间件基类
/// </summary>
public abstract class PipeTransformMiddlewareBase : IPipeTransformMiddleware
{
    public virtual DataContext Pass(DataContext context) => context;
    public virtual Task<DataContext> PassAsync(DataContext context) => Task.FromResult(Pass(context));
    public dynamic GetMetadata()
    {
        return new
        {
            GetType().Name,
            GetType().FullName,
            GetType().AssemblyQualifiedName
        };
    }
}

/// <summary>
/// 监控中间件基类
/// </summary>
public abstract class PipeMonitorMiddlewareBase : IPipeMonitorMiddleware
{
    public virtual DataContext Pass(DataContext context) => context;
    public virtual Task<DataContext> PassAsync(DataContext context) => Task.FromResult(Pass(context));
    public dynamic GetMetadata()
    {
        return new
        {
            GetType().Name,
            GetType().FullName,
            GetType().AssemblyQualifiedName
        };
    }

    public DataPipeline Pipe { get; set; }
}