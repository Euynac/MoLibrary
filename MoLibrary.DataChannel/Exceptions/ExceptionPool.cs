using System.Collections.Concurrent;

namespace MoLibrary.DataChannel.Exceptions;

/// <summary>
/// 异常池管理类
/// 提供固定大小的异常缓冲池，自动移除最旧的异常记录
/// </summary>
public class ExceptionPool
{
    private readonly int _maxSize;
    private readonly ConcurrentQueue<PipelineException> _exceptions;
    private readonly ReaderWriterLockSlim _lock;
    private int _totalExceptionCount;

    /// <summary>
    /// 管道ID
    /// </summary>
    public string PipelineId { get; private set; }

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
    /// 初始化异常池
    /// </summary>
    /// <param name="pipelineId">管道ID</param>
    /// <param name="maxSize">异常池最大容量</param>
    public ExceptionPool(string pipelineId, int maxSize = 10)
    {
        if (string.IsNullOrEmpty(pipelineId))
        {
            throw new ArgumentException("管道ID不能为空", nameof(pipelineId));
        }
        
        if (maxSize <= 0)
        {
            throw new ArgumentException("异常池大小必须大于0", nameof(maxSize));
        }
        
        PipelineId = pipelineId;
        _maxSize = maxSize;
        _exceptions = new ConcurrentQueue<PipelineException>();
        _lock = new ReaderWriterLockSlim();
        _totalExceptionCount = 0;
    }

    /// <summary>
    /// 添加异常到池中
    /// 如果池已满，将移除最旧的异常
    /// </summary>
    /// <param name="exception">异常对象</param>
    /// <param name="source">异常来源对象</param>
    public void AddException(Exception exception, object source)
    {
        if (exception == null) return;

        var pipelineException = new PipelineException(exception, source);
        
        _lock.EnterWriteLock();
        try
        {
            _exceptions.Enqueue(pipelineException);
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
    }

    /// <summary>
    /// 获取所有异常记录
    /// </summary>
    /// <returns>异常记录列表，按时间倒序排列（最新的在前）</returns>
    public IReadOnlyList<PipelineException> GetExceptions()
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
    public IReadOnlyList<PipelineException> GetRecentExceptions(int count)
    {
        if (count <= 0) return new List<PipelineException>();
        
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