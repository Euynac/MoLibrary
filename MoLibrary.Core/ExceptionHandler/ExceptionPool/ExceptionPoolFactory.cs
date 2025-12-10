using Microsoft.Extensions.Options;

namespace MoLibrary.Core.ExceptionHandler.ExceptionPool;

/// <summary>
/// 异常池工厂类
/// 负责创建异常池实例，并自动合并全局配置和实例配置
/// </summary>
/// <typeparam name="TException">异常记录类型，必须继承自 PooledException</typeparam>
public class ExceptionPoolFactory<TException> where TException : PooledException
{
    private readonly IOptions<ExceptionPoolGlobalOption> _globalOptions;

    /// <summary>
    /// 初始化异常池工厂
    /// </summary>
    /// <param name="globalOptions">全局配置选项</param>
    public ExceptionPoolFactory(IOptions<ExceptionPoolGlobalOption> globalOptions)
    {
        _globalOptions = globalOptions;
    }

    /// <summary>
    /// 创建异常池实例
    /// </summary>
    /// <param name="poolId">异常池ID</param>
    /// <param name="configure">实例配置委托，可选</param>
    /// <returns>配置好的异常池实例</returns>
    public ExceptionPool<TException> Create(string poolId, Action<ExceptionPoolOption>? configure = null)
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
        var globalOption = _globalOptions.Value;
        var maxSize = instanceOption.MaxSize ?? globalOption.DefaultMaxSize;
        var enableEventTrigger = instanceOption.EnableEventTrigger ?? globalOption.EnableEventTrigger;

        // 创建并返回异常池实例
        return new ExceptionPool<TException>(poolId, maxSize, enableEventTrigger);
    }
}
