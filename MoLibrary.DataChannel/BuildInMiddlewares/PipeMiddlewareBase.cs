using System.Collections.Concurrent;
using MoLibrary.DataChannel.Pipeline;

namespace MoLibrary.DataChannel.BuildInMiddlewares;

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

    public DataPipeline Pipe { get; set; } = null!;
}

/// <summary>
/// 信息展示中间件基类
/// 提供并发字典用于存储和展示统计信息，专门用于UI管理界面展示
/// 开发者可以继承此类并根据消息传递写入信息到字典中
/// </summary>
public abstract class PipeInfoDisplayMiddlewareBase : PipeMonitorMiddlewareBase
{
    /// <summary>
    /// 信息展示字典，用于存储各种统计信息
    /// Key: 信息标识
    /// Value: 信息内容（支持各种类型）
    /// </summary>
    protected readonly ConcurrentDictionary<string, object> InfoDictionary = new();

    /// <summary>
    /// 获取所有信息字典的只读副本
    /// </summary>
    /// <returns>信息字典的只读集合</returns>
    public IReadOnlyDictionary<string, object> GetInfoDictionary()
    {
        return InfoDictionary.AsReadOnly();
    }

    /// <summary>
    /// 清空信息字典
    /// </summary>
    public void ClearInfo()
    {
        InfoDictionary.Clear();
    }

    /// <summary>
    /// 设置信息项
    /// </summary>
    /// <param name="key">信息键</param>
    /// <param name="value">信息值</param>
    protected void SetInfo(string key, object value)
    {
        InfoDictionary.AddOrUpdate(key, value, (_, _) => value);
    }

    /// <summary>
    /// 获取信息项
    /// </summary>
    /// <param name="key">信息键</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>信息值</returns>
    protected T GetInfo<T>(string key, T defaultValue = default!)
    {
        return InfoDictionary.TryGetValue(key, out var value) && value is T typedValue ? typedValue : defaultValue;
    }

    /// <summary>
    /// 增加计数器
    /// </summary>
    /// <param name="key">计数器键</param>
    /// <param name="increment">增量，默认为1</param>
    /// <returns>增加后的值</returns>
    protected long IncrementCounter(string key, long increment = 1)
    {
        return InfoDictionary.AddOrUpdate(key, increment, (_, existingValue) =>
        {
            if (existingValue is long longValue)
                return longValue + increment;
            if (existingValue is int intValue)
                return intValue + increment;
            return increment;
        }) as long? ?? increment;
    }

    /// <summary>
    /// 重置计数器
    /// </summary>
    /// <param name="key">计数器键</param>
    protected void ResetCounter(string key)
    {
        InfoDictionary.AddOrUpdate(key, 0L, (_, _) => 0L);
    }

    /// <summary>
    /// 重写元数据方法，包含信息展示标识
    /// </summary>
    /// <returns>包含信息展示标识的元数据</returns>
    public new dynamic GetMetadata()
    {
        var baseMetadata = base.GetMetadata();
        return new
        {
            baseMetadata.Name,
            baseMetadata.FullName,
            baseMetadata.AssemblyQualifiedName,
            IsInfoDisplayMiddleware = true
        };
    }
}