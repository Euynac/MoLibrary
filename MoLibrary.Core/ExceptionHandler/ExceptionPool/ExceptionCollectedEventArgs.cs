namespace MoLibrary.Core.ExceptionHandler.ExceptionPool;

/// <summary>
/// 异常收集事件参数
/// 当异常被添加到异常池时触发
/// </summary>
/// <typeparam name="TException">异常记录类型</typeparam>
public class ExceptionCollectedEventArgs<TException> : EventArgs where TException : PooledException
{
    /// <summary>
    /// 收集的异常记录
    /// </summary>
    public TException PooledException { get; }

    /// <summary>
    /// 异常池ID
    /// </summary>
    public string PoolId { get; }

    /// <summary>
    /// 发生的异常（便捷访问器）
    /// </summary>
    public Exception Exception => PooledException.Exception;

    /// <summary>
    /// 异常来源对象（便捷访问器）
    /// </summary>
    public object Source => PooledException.Source;

    /// <summary>
    /// 异常描述信息（便捷访问器）
    /// </summary>
    public string? Description => PooledException.Description;

    /// <summary>
    /// 初始化异常收集事件参数
    /// </summary>
    /// <param name="pooledException">收集的异常记录</param>
    /// <param name="poolId">异常池ID</param>
    public ExceptionCollectedEventArgs(TException pooledException, string poolId)
    {
        PooledException = pooledException;
        PoolId = poolId;
    }
}
