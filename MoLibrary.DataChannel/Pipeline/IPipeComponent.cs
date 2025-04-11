namespace MoLibrary.DataChannel.Pipeline;

/// <summary>
/// 管道组件基础接口
/// 所有管道相关组件（端点、中间件等）的公共接口
/// 提供元数据访问的基本功能
/// </summary>
public interface IPipeComponent
{
    /// <summary>
    /// 获取管道组件元数据
    /// 返回组件的配置数据、状态信息等动态属性
    /// </summary>
    /// <returns>包含组件元数据的动态对象</returns>
    public dynamic GetMetadata();
}