using System.Collections.Concurrent;

namespace MoLibrary.Core.ExceptionHandler.ExceptionPool;

/// <summary>
/// 泛型异常池管理类
/// 提供固定大小的异常缓冲池，自动移除最旧的异常记录
/// 支持事件通知和自定义异常类型
/// </summary>
/// <typeparam name="TException">异常记录类型，必须继承自 PooledException</typeparam>
public class ExceptionPool<TException> where TException : PooledException
{
    private readonly int _maxSize;
    private readonly bool _enableEventTrigger;
    private readonly ConcurrentQueue<TException> _exceptions;
    private readonly ReaderWriterLockSlim _lock;
    private int _totalExceptionCount;

    /// <summary>
    /// 异常池ID
    /// </summary>
    public string PoolId { get; private set; }

    /// <summary>
    /// 获取当前异常数量
    /// </summary>
    public int Count => _exceptions.Count;

    /// <summary>
    /// 获取异常池最大容量
    /// </summary>
    public int MaxSize => _maxSize;

    /// <summary>
    /// 获取总异常数量（包括已被移除的异常）
    /// </summary>
    public int TotalExceptionCount
    {
        get
        {
            _lock.EnterReadLock();
            try
            {
                return _totalExceptionCount;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }

    /// <summary>
    /// 获取是否存在异常
    /// </summary>
    public bool HasExceptions => _exceptions.Count > 0;

    /// <summary>
    /// 异常收集事件，当异常被添加到异常池时触发
    /// </summary>
    public event EventHandler<ExceptionCollectedEventArgs<TException>>? ExceptionCollected;

    /// <summary>
    /// 初始化异常池
    /// </summary>
    /// <param name="poolId">异常池ID</param>
    /// <param name="maxSize">异常池最大容量</param>
    /// <param name="enableEventTrigger">是否启用事件触发</param>
    public ExceptionPool(string poolId, int maxSize, bool enableEventTrigger = true)
    {
        if (string.IsNullOrEmpty(poolId))
        {
            throw new ArgumentException("异常池ID不能为空", nameof(poolId));
        }

        if (maxSize <= 0)
        {
            throw new ArgumentException("异常池大小必须大于0", nameof(maxSize));
        }

        PoolId = poolId;
        _maxSize = maxSize;
        _enableEventTrigger = enableEventTrigger;
        _exceptions = new ConcurrentQueue<TException>();
        _lock = new ReaderWriterLockSlim();
        _totalExceptionCount = 0;
    }

    /// <summary>
    /// 添加异常到池中
    /// 如果池已满，将移除最旧的异常
    /// </summary>
    /// <param name="exception">异常对象</param>
    /// <param name="source">异常来源对象</param>
    /// <param name="description">异常描述信息</param>
    public void AddException(Exception exception, object source, string? description = null)
    {
        // 创建异常记录实例
        var pooledException = (TException)Activator.CreateInstance(
            typeof(TException),
            exception,
            source,
            description)!;

        _lock.EnterWriteLock();
        try
        {
            _exceptions.Enqueue(pooledException);
            _totalExceptionCount++;

            // 如果超过最大容量，移除最旧的异常
            while (_exceptions.Count > _maxSize)
            {
                _exceptions.TryDequeue(out _);
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }

        // 触发异常收集事件
        if (_enableEventTrigger)
        {
            var eventArgs = new ExceptionCollectedEventArgs<TException>(pooledException, PoolId);
            ExceptionCollected?.Invoke(this, eventArgs);
        }
    }

    /// <summary>
    /// 获取所有异常记录
    /// </summary>
    /// <returns>异常记录列表，按时间倒序排列（最新的在前）</returns>
    public IReadOnlyList<TException> GetExceptions()
    {
        _lock.EnterReadLock();
        try
        {
            return _exceptions.ToArray()
                .OrderByDescending(e => e.Timestamp)
                .ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// 获取最近的异常记录
    /// </summary>
    /// <param name="count">要获取的异常数量</param>
    /// <returns>最近的异常记录列表</returns>
    public IReadOnlyList<TException> GetRecentExceptions(int count)
    {
        if (count <= 0) return new List<TException>();

        _lock.EnterReadLock();
        try
        {
            return _exceptions.ToArray()
                .OrderByDescending(e => e.Timestamp)
                .Take(count)
                .ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// 清空异常池
    /// </summary>
    public void Clear()
    {
        _lock.EnterWriteLock();
        try
        {
            while (_exceptions.TryDequeue(out _))
            {
                // 清空队列
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        _lock?.Dispose();
    }
}
