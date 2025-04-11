using System.Dynamic;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.DataChannel.Pipeline;

/// <summary>
/// 数据上下文类
/// 用于在数据传输和处理过程中包装和携带数据及其元数据
/// 作为数据管道中的主要传输单元
/// </summary>
public class DataContext
{
    /// <summary>
    /// 初始化DataContext的新实例
    /// </summary>
    /// <param name="source">数据来源</param>
    /// <param name="data">数据内容</param>
    public DataContext(EDataSource source, object? data)
    {
        Source = source;
        Data = data;
    }
    
    /// <summary>
    /// 消息数据进入入口
    /// 表示数据从哪个端点进入管道，仅Inner或Outer
    /// </summary>
    public EDataSource Source { get; set; }
   
    /// <summary>
    /// 元数据
    /// 用于存储额外的上下文信息，可由Endpoint或中间件进行解析和使用
    /// </summary>
    public ExpandoObject Metadata { get; set; } = new();
    
    /// <summary>
    /// 数据对象
    /// 实际传输的数据内容
    /// </summary>
    public object? Data { get; set; }

    /// <summary>
    /// 数据CLR类型
    /// </summary>
    public Type? DataType => Data?.GetType();
    //TODO 处理ERROR

    /// <summary>
    /// 复制数据上下文元数据
    /// 将指定数据上下文的元数据复制到当前实例
    /// </summary>
    /// <param name="data">源数据上下文</param>
    /// <returns>当前数据上下文实例</returns>
    public DataContext CopyMetadata(DataContext data)
    {
        Metadata.Copy(data.Metadata);
        return this;
    }
}

/// <summary>
/// 数据来源枚举，决定数据在消息通路中的流向。如数据来源于内部端点则由Outer端点接收。
/// </summary>
public enum EDataSource
{
    /// <summary>
    /// 数据来源于内部端点
    /// </summary>
    Inner,
    
    /// <summary>
    /// 数据来源于外部端点
    /// </summary>
    Outer
}
