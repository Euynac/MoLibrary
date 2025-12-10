using Microsoft.Extensions.Options;

namespace MoLibrary.Core.ExceptionHandler.ExceptionPool;

/// <summary>
/// 异常池管理器实现类
/// 负责创建和管理异常池实例
/// </summary>
public class ExceptionPoolManager(IOptions<ExceptionPoolGlobalOption> globalOptions) : IExceptionPoolManager
{
    /// <summary>
    /// 创建泛型异常池实例
    /// </summary>
    /// <typeparam name="TException">异常记录类型，必须继承自 PooledException</typeparam>
    /// <param name="poolId">异常池ID</param>
    /// <param name="configure">实例配置委托，可选</param>
    /// <returns>配置好的泛型异常池实例</returns>
    public ExceptionPool<TException> Create<TException>(string poolId, Action<ExceptionPoolOption>? configure = null)
        where TException : PooledException
    {
        if (string.IsNullOrEmpty(poolId))
        {
            throw new ArgumentException("异常池ID不能为空", nameof(poolId));
        }

        // 创建实例配置选项
        var instanceOption = new ExceptionPoolOption { PoolId = poolId };

        // 应用用户配置
        configure?.Invoke(instanceOption);

        // 合并全局和实例配置（实例配置优先）
        var globalOption = globalOptions.Value;
        var maxSize = instanceOption.MaxSize ?? globalOption.DefaultMaxSize;
        var enableEventTrigger = instanceOption.EnableEventTrigger ?? globalOption.EnableEventTrigger;

        // 创建并返回异常池实例
        return (ExceptionPool<TException>)Activator.CreateInstance(
            typeof(ExceptionPool<TException>),
            poolId,
            maxSize,
            enableEventTrigger)!;
    }

    /// <summary>
    /// 创建非泛型异常池实例
    /// 使用默认的 PooledException 作为异常记录类型
    /// </summary>
    /// <param name="poolId">异常池ID</param>
    /// <param name="configure">实例配置委托，可选</param>
    /// <returns>配置好的非泛型异常池实例</returns>
    public ExceptionPool Create(string poolId, Action<ExceptionPoolOption>? configure = null)
    {
        if (string.IsNullOrEmpty(poolId))
        {
            throw new ArgumentException("异常池ID不能为空", nameof(poolId));
        }

        // 创建实例配置选项
        var instanceOption = new ExceptionPoolOption { PoolId = poolId };

        // 应用用户配置
        configure?.Invoke(instanceOption);

        // 合并全局和实例配置（实例配置优先）
        var globalOption = globalOptions.Value;
        var maxSize = instanceOption.MaxSize ?? globalOption.DefaultMaxSize;
        var enableEventTrigger = instanceOption.EnableEventTrigger ?? globalOption.EnableEventTrigger;

        // 创建并返回非泛型异常池实例
        return new ExceptionPool(poolId, maxSize, enableEventTrigger);
    }
}
