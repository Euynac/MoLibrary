namespace MoLibrary.Core.ExceptionHandler.ExceptionPool;

/// <summary>
/// 异常池管理器接口
/// 提供创建和管理异常池的统一接口
/// </summary>
public interface IExceptionPoolManager
{
    /// <summary>
    /// 创建泛型异常池实例
    /// </summary>
    /// <typeparam name="TException">异常记录类型，必须继承自 PooledException</typeparam>
    /// <param name="poolId">异常池ID</param>
    /// <param name="configure">实例配置委托，可选</param>
    /// <returns>配置好的泛型异常池实例</returns>
    ExceptionPool<TException> Create<TException>(string poolId, Action<ExceptionPoolOption>? configure = null)
        where TException : PooledException;

    /// <summary>
    /// 创建非泛型异常池实例
    /// 使用默认的 PooledException 作为异常记录类型
    /// </summary>
    /// <param name="poolId">异常池ID</param>
    /// <param name="configure">实例配置委托，可选</param>
    /// <returns>配置好的非泛型异常池实例</returns>
    ExceptionPool Create(string poolId, Action<ExceptionPoolOption>? configure = null);
}
