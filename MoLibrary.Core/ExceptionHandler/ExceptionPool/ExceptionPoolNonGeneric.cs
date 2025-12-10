namespace MoLibrary.Core.ExceptionHandler.ExceptionPool;

/// <summary>
/// 非泛型异常池类
/// 继承自 <see cref="ExceptionPool{PooledException}"/>，提供默认异常类型的异常池
/// </summary>
public class ExceptionPool : ExceptionPool<PooledException>
{
    /// <summary>
    /// 初始化非泛型异常池
    /// </summary>
    /// <param name="poolId">异常池ID</param>
    /// <param name="maxSize">异常池最大容量</param>
    /// <param name="enableEventTrigger">是否启用事件触发</param>
    public ExceptionPool(string poolId, int maxSize, bool enableEventTrigger = true)
        : base(poolId, maxSize, enableEventTrigger)
    {
    }
}
