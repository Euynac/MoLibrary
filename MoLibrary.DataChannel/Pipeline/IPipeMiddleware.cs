using MoLibrary.DataChannel.Interfaces;

namespace MoLibrary.DataChannel.Pipeline;

/// <summary>
/// （中间件是单例生命周期，或通过 <see cref="IComponentTransient"/> 声明为Transient生命周期的中间件）管道中间件基础接口
/// 所有管道中间件的基本接口，表示可以参与数据管道处理链的组件
/// 中间件可以拦截、修改或转换数据流
/// </summary>
public interface IPipeMiddleware : IPipeComponent
{
}

/// <summary>
/// 管道转换中间件接口
/// 负责对管道中流动的数据进行变换、处理或过滤
/// 每个数据上下文都会按顺序通过所有转换中间件
/// </summary>
public interface IPipeTransformMiddleware : IPipeMiddleware
{
    /// <summary>
    /// 数据上下文通过管道中间件
    /// 对传入的数据上下文进行处理，并返回处理后的结果
    /// </summary>
    /// <param name="context">要处理的数据上下文</param>
    /// <returns>处理后的数据上下文</returns>
    public Task<DataContext> PassAsync(DataContext context);
}

/// <summary>
/// 管道端点中间件接口
/// 专门用于处理和配置端点的中间件
/// 可以增强或修改端点的行为
/// </summary>
public interface IPipeEndpointMiddleware : IPipeMiddleware, IWantAccessPipeline
{
}

/// <summary>
/// 管道监控中间件接口
/// 提供对管道的全方位监控和控制功能
/// 结合了转换能力和管道访问能力
/// </summary>
public interface IPipeMonitorMiddleware : IPipeTransformMiddleware, IWantAccessPipeline
{
}
