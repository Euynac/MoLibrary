using BuildingBlocksPlatform.DataChannel.Pipeline;

namespace BuildingBlocksPlatform.DataChannel.Interfaces;

/// <summary>
/// 注入管道实例。Middleware与Endpoint均适用
/// </summary>
public interface IWantAccessPipeline
{
    /// <summary>
    /// 管道实例
    /// </summary>
    public DataPipeline Pipe { get; set; }
}