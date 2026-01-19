using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.Extensions.Logging;
using MoLibrary.Profiling.Models;

namespace MoLibrary.Profiling.Services;

/// <summary>
///     类型分配收集器 - 使用 TraceEvent/ETW 的 GCAllocationTick 事件收集类型级别的分配信息
/// </summary>
/// <remarks>
///     使用 GCAllocationTick 事件而非 GCSampledObjectAllocation，因为:
///     1. GCAllocationTick 直接提供 TypeName 属性，无需 TypeBulkType 查找
///     2. GCSampledObjectAllocation 依赖 TypeBulkType 事件提供 TypeID → TypeName 映射，
///        但在自监控场景下 TypeBulkType.TypeName 可能为空（时序问题）
///     3. GCAllocationTick 在每约 100KB 分配时触发，提供可靠的采样
/// </remarks>
public class TypeAllocationCollector : IDisposable
{
    private readonly ILogger<TypeAllocationCollector> _logger;

    /// <summary>
    ///     TypeName -> AllocationInfo 分配统计
    /// </summary>
    private readonly ConcurrentDictionary<string, TypeAllocationInfo> _typeAllocations = new();

    /// <summary>
    ///     快照历史记录
    /// </summary>
    private readonly ConcurrentQueue<TypeAllocationSnapshot> _history = new();

    /// <summary>
    ///     排除模式 (编译后的正则)
    /// </summary>
    private readonly List<Regex> _excludePatterns;

    // ETW 会话相关
    private TraceEventSession? _session;
    private ETWTraceEventSource? _source;
    private Task? _processingTask;
    private CancellationTokenSource? _cts;

    // 状态
    private volatile bool _isCollecting;
    private AllocationSamplingMode _currentMode = AllocationSamplingMode.Disabled;
    private DateTime _collectionStartTime;
    private long _totalAllocationCount;
    private long _totalBytes;
    private long _droppedEventCount;

    // 快照定时器
    private Timer? _snapshotTimer;

    // 配置
    private readonly int _maxTrackedTypes;
    private readonly TimeSpan _snapshotInterval;
    private readonly int _maxHistorySnapshots;
    private readonly int _etwBufferSizeMB;
    private readonly TimeSpan? _autoStopAfter;

    /// <summary>
    ///     当新快照可用时触发
    /// </summary>
    public event Action<TypeAllocationSnapshot>? OnSnapshotUpdated;

    /// <summary>
    ///     是否正在收集
    /// </summary>
    public bool IsCollecting => _isCollecting;

    /// <summary>
    ///     当前采样模式
    /// </summary>
    public AllocationSamplingMode CurrentMode => _currentMode;

    public TypeAllocationCollector(
        ILogger<TypeAllocationCollector> logger,
        int maxTrackedTypes = 500,
        TimeSpan? snapshotInterval = null,
        int maxHistorySnapshots = 60,
        int etwBufferSizeMB = 64,
        TimeSpan? autoStopAfter = null,
        IEnumerable<string>? excludeTypePatterns = null)
    {
        _logger = logger;
        _maxTrackedTypes = maxTrackedTypes;
        _snapshotInterval = snapshotInterval ?? TimeSpan.FromSeconds(2);
        _maxHistorySnapshots = maxHistorySnapshots;
        _etwBufferSizeMB = etwBufferSizeMB;
        _autoStopAfter = autoStopAfter ?? TimeSpan.FromMinutes(10);

        // 编译排除模式
        var patterns = excludeTypePatterns ?? ["^System\\.Runtime\\."];
        _excludePatterns = patterns
            .Select(p => new Regex(p, RegexOptions.Compiled | RegexOptions.IgnoreCase))
            .ToList();
    }

    /// <summary>
    ///     开始收集分配事件
    /// </summary>
    /// <param name="mode">采样模式</param>
    public void StartCollection(AllocationSamplingMode mode)
    {
        if (_isCollecting)
        {
            _logger.LogWarning("分配收集已在进行中");
            return;
        }

        // 检查平台支持
        if (!OperatingSystem.IsWindows())
        {
            _logger.LogWarning("ETW 分配跟踪仅在 Windows 上完全支持，当前平台可能功能受限");
        }

        try
        {
            _currentMode = mode;
            _collectionStartTime = DateTime.UtcNow;
            _cts = new CancellationTokenSource();

            // 创建 ETW 会话
            var sessionName = $"MoLibrary_TypeAllocation_{Guid.NewGuid():N}"[..50];
            _session = new TraceEventSession(sessionName, TraceEventSessionOptions.Create)
            {
                BufferSizeMB = _etwBufferSizeMB
            };

            // 使用 GC 关键字启用 GCAllocationTick 事件
            // GCAllocationTick 直接提供 TypeName，无需 TypeBulkType 查找
            var keywords = ClrTraceEventParser.Keywords.GC;

            // 启用 CLR Provider
            _session.EnableProvider(
                ClrTraceEventParser.ProviderGuid,
                TraceEventLevel.Verbose,
                (ulong)keywords);

            // 获取事件源并订阅 GCAllocationTick 事件
            _source = _session.Source;
            _source.Clr.GCAllocationTick += OnAllocationTick;

            // 在后台线程处理事件
            _processingTask = Task.Run(() =>
            {
                try
                {
                    _source.Process();
                }
                catch (Exception ex) when (!_cts.Token.IsCancellationRequested)
                {
                    _logger.LogError(ex, "处理 ETW 事件时出错");
                }
            }, _cts.Token);

            // 启动快照定时器
            _snapshotTimer = new Timer(
                CaptureSnapshot,
                null,
                _snapshotInterval,
                _snapshotInterval);

            // 自动停止定时器
            if (_autoStopAfter.HasValue)
            {
                _ = Task.Delay(_autoStopAfter.Value, _cts.Token)
                    .ContinueWith(_ =>
                    {
                        _logger.LogInformation("达到自动停止时间，正在停止收集");
                        StopCollection();
                    }, TaskContinuationOptions.OnlyOnRanToCompletion);
            }

            _isCollecting = true;
            _logger.LogInformation("类型分配收集已启动，模式: {Mode}", mode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动类型分配收集失败");
            Cleanup();
            throw;
        }
    }

    /// <summary>
    ///     停止收集
    /// </summary>
    public void StopCollection()
    {
        if (!_isCollecting) return;

        _isCollecting = false;
        _currentMode = AllocationSamplingMode.Disabled;

        Cleanup();

        _logger.LogInformation("类型分配收集已停止");
    }

    private void Cleanup()
    {
        _snapshotTimer?.Dispose();
        _snapshotTimer = null;

        _cts?.Cancel();

        try
        {
            _session?.Stop();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "停止 ETW 会话时出错");
        }

        _session?.Dispose();
        _session = null;
        _source = null;

        _cts?.Dispose();
        _cts = null;
    }

    /// <summary>
    ///     处理 GCAllocationTick 事件 - 直接获取 TypeName
    /// </summary>
    /// <remarks>
    ///     GCAllocationTick 在每约 100KB 分配时触发，提供:
    ///     - TypeName: 直接可用的类型名称
    ///     - AllocationAmount: 本次分配大小
    ///     - AllocationKind: SOH(0)/LOH(1)/POH(2)
    /// </remarks>
    private void OnAllocationTick(GCAllocationTickTraceData data)
    {
        var typeName = data.TypeName;

        // 跳过空类型名
        if (string.IsNullOrEmpty(typeName))
        {
            Interlocked.Increment(ref _droppedEventCount);
            return;
        }

        // 检查排除模式
        if (ShouldExcludeType(typeName))
            return;

        // 更新聚合数据
        var info = _typeAllocations.GetOrAdd(typeName, name => new TypeAllocationInfo
        {
            TypeName = name,
            LastSeenUtc = DateTime.UtcNow
        });

        // 线程安全更新
        // AllocationTick: 每次事件代表一次分配，AllocationAmount 是分配大小
        info.AddAllocationCount(1);
        info.AddTotalBytes(data.AllocationAmount);
        info.LastSeenUtc = DateTime.UtcNow;

        Interlocked.Add(ref _totalAllocationCount, 1);
        Interlocked.Add(ref _totalBytes, data.AllocationAmount);

        // 强制执行最大跟踪类型数
        if (_typeAllocations.Count > _maxTrackedTypes)
        {
            PruneSmallestTypes();
        }
    }

    /// <summary>
    ///     检查是否应该排除该类型
    /// </summary>
    private bool ShouldExcludeType(string typeName)
    {
        foreach (var pattern in _excludePatterns)
        {
            if (pattern.IsMatch(typeName))
                return true;
        }
        return false;
    }

    /// <summary>
    ///     修剪最小的类型以保持在限制内
    /// </summary>
    private void PruneSmallestTypes()
    {
        var toRemove = _typeAllocations
            .OrderBy(kvp => kvp.Value.TotalBytes)
            .Take(_typeAllocations.Count - _maxTrackedTypes + 50)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in toRemove)
        {
            _typeAllocations.TryRemove(key, out _);
        }
    }

    /// <summary>
    ///     捕获快照
    /// </summary>
    private void CaptureSnapshot(object? state)
    {
        if (!_isCollecting) return;

        var snapshot = BuildSnapshot();

        _history.Enqueue(snapshot);
        while (_history.Count > _maxHistorySnapshots)
        {
            _history.TryDequeue(out _);
        }

        OnSnapshotUpdated?.Invoke(snapshot);
    }

    /// <summary>
    ///     构建当前快照
    /// </summary>
    private TypeAllocationSnapshot BuildSnapshot()
    {
        var totalCount = Interlocked.Read(ref _totalAllocationCount);
        var totalBytes = Interlocked.Read(ref _totalBytes);

        var types = _typeAllocations.Values
            .Select(t => new TypeAllocationInfo
            {
                TypeName = t.TypeName,
                AllocationCount = t.AllocationCount,
                TotalBytes = t.TotalBytes,
                AllocationPercentage = totalCount > 0 ? (double)t.AllocationCount / totalCount * 100 : 0,
                BytesPercentage = totalBytes > 0 ? (double)t.TotalBytes / totalBytes * 100 : 0,
                LastSeenUtc = t.LastSeenUtc
            })
            .OrderByDescending(t => t.TotalBytes)
            .Take(_maxTrackedTypes)
            .ToList();

        return new TypeAllocationSnapshot
        {
            Timestamp = DateTime.UtcNow,
            Duration = _isCollecting ? DateTime.UtcNow - _collectionStartTime : TimeSpan.Zero,
            Types = types,
            TotalAllocationCount = totalCount,
            TotalBytes = totalBytes,
            IsCollecting = _isCollecting,
            SamplingMode = _currentMode,
            DroppedEventCount = Interlocked.Read(ref _droppedEventCount),
            DataSource = TypeAllocationDataSource.AllocationTracking
        };
    }

    /// <summary>
    ///     获取当前快照
    /// </summary>
    public TypeAllocationSnapshot GetCurrentSnapshot()
    {
        return BuildSnapshot();
    }

    /// <summary>
    ///     获取历史快照
    /// </summary>
    public IReadOnlyList<TypeAllocationSnapshot> GetHistory()
    {
        return _history.ToList();
    }

    /// <summary>
    ///     重置数据
    /// </summary>
    public void ResetData()
    {
        _typeAllocations.Clear();
        while (_history.TryDequeue(out _)) { }
        Interlocked.Exchange(ref _totalAllocationCount, 0);
        Interlocked.Exchange(ref _totalBytes, 0);
        Interlocked.Exchange(ref _droppedEventCount, 0);
        _collectionStartTime = DateTime.UtcNow;
    }

    public void Dispose()
    {
        StopCollection();
        _typeAllocations.Clear();
        GC.SuppressFinalize(this);
    }
}
