using BuildingBlocksPlatform.DataChannel.Interfaces;

namespace BuildingBlocksPlatform.DataChannel.Pipeline;

/// <summary>
/// 管道中间件接口
/// </summary>
public interface IPipeMiddleware : IPipeComponent
{

}

/// <summary>
/// 管道转换中间件。对管道数据传输做变换。
/// </summary>
public interface IPipeTransformMiddleware : IPipeMiddleware
{
    /// <summary>
    /// DataContext经过管道中间件
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public Task<DataContext> PassAsync(DataContext context);
}

/// <summary>
/// 管道端点中间件。对端点进行额外配置等。
/// </summary>
public interface IPipeEndpointMiddleware : IPipeMiddleware, IWantAccessPipeline
{

}

/// <summary>
/// 管道监控中间件。对管道进行全方位的控制
/// </summary>
public interface IPipeMonitorMiddleware : IPipeTransformMiddleware, IWantAccessPipeline
{

}
