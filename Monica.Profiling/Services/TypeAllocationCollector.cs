using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.Extensions.Logging;
using Monica.Profiling.Models;
using Monica.Tool.Extensions;

namespace Monica.Profiling.Services;

/// <summary>
/// Type Allocation Collector - Collect type-level allocation information using the GCAllocationTick event of TraceEvent/ETW
/// </summary>
/// <remarks>
/// Use the GCAllocationTick event instead of GCSampledObjectAllocation because:
/// 1. GCAllocationTick directly provides the TypeName property without the need for TypeBulkType lookup
/// 2. GCSampledObjectAllocation relies on the TypeBulkType event to provide TypeID → TypeName mapping.
/// But in the self-monitoring scenario, TypeBulkType.TypeName may be empty (timing issue)
/// 3. GCAllocationTick triggers every ~100KB allocation, providing reliable sampling
/// </remarks>
public class TypeAllocationCollector : IDisposable
{
    private readonly ILogger<TypeAllocationCollector> _logger;

    /// <summary>
    /// TypeName -> AllocationInfo allocation statistics
    /// </summary>
    private readonly ConcurrentDictionary<string, TypeAllocationInfo> _typeAllocations = new();

    /// <summary>
    /// Snapshot history
    /// </summary>
    private readonly ConcurrentQueue<TypeAllocationSnapshot> _history = new();

    /// <summary>
    /// Exclusion patterns (compiled regular expressions)
    /// </summary>
    private readonly List<Regex> _excludePatterns;

    // ETW session related
    private TraceEventSession? _session;
    private ETWTraceEventSource? _source;
    private Task? _processingTask;
    private CancellationTokenSource? _cts;

    // state
    private volatile bool _isCollecting;
    private AllocationSamplingMode _currentMode = AllocationSamplingMode.Disabled;
    private DateTime _collectionStartTime;
    private long _totalAllocationCount;
    private long _totalBytes;
    private long _droppedEventCount;

    // snapshot timer
    private Timer? _snapshotTimer;

    // Configuration
    private readonly int _maxTrackedTypes;
    private readonly TimeSpan _snapshotInterval;
    private readonly int _maxHistorySnapshots;
    private readonly int _etwBufferSizeMB;
    private readonly TimeSpan? _autoStopAfter;

    /// <summary>
    /// Triggered when a new snapshot is available
    /// </summary>
    public event Action<TypeAllocationSnapshot>? OnSnapshotUpdated;

    /// <summary>
    /// Is collecting
    /// </summary>
    public bool IsCollecting => _isCollecting;

    /// <summary>
    /// Current sampling mode
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

        // compile exclude mode
        var patterns = excludeTypePatterns ?? ["^System\\.Runtime\\."];
        _excludePatterns = patterns
            .Select(p => new Regex(p, RegexOptions.Compiled | RegexOptions.IgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Start collecting distribution events
    /// </summary>
    /// <param name="mode">Sampling mode</param>
    public void StartCollection(AllocationSamplingMode mode)
    {
        if (_isCollecting)
        {
            _logger.LogWarning("分配收集已在进行中");
            return;
        }

        // Check platform support
        if (!OperatingSystem.IsWindows())
        {
            _logger.LogWarning("ETW 分配跟踪仅在 Windows 上完全支持，当前平台可能功能受限");
        }

        try
        {
            _currentMode = mode;
            _collectionStartTime = DateTime.UtcNow;
            _cts = new CancellationTokenSource();

            // Create an ETW session
            var sessionName = $"Monica_TypeAllocation_{Guid.NewGuid():N}"[..50];
            _session = new TraceEventSession(sessionName, TraceEventSessionOptions.Create)
            {
                BufferSizeMB = _etwBufferSizeMB
            };

            // Enable the GCAllocationTick event using the GC keyword
            // GCAllocationTick provides TypeName directly, no TypeBulkType lookup required
            var keywords = ClrTraceEventParser.Keywords.GC;

            // Enable CLR Provider
            _session.EnableProvider(
                ClrTraceEventParser.ProviderGuid,
                TraceEventLevel.Verbose,
                (ulong)keywords);

            // Get the event source and subscribe to the GCAllocationTick event
            _source = _session.Source;
            _source.Clr.GCAllocationTick += OnAllocationTick;

            // Handling events on a background thread
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

            // Start snapshot timer
            _snapshotTimer = new Timer(
                CaptureSnapshot,
                null,
                _snapshotInterval,
                _snapshotInterval);

            // Auto stop timer
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
    /// Stop collection
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

        _cts.SafeCancelAndDispose();
        _cts = null;

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
    }

    /// <summary>
    /// Handle GCAllocationTick event - get TypeName directly
    /// </summary>
    /// <remarks>
    /// GCAllocationTick fires every ~100KB allocation, providing:
    /// - TypeName: Directly available type name
    /// - AllocationAmount: the size of this allocation
    ///     - AllocationKind: SOH(0)/LOH(1)/POH(2)
    /// </remarks>
    private void OnAllocationTick(GCAllocationTickTraceData data)
    {
        var typeName = data.TypeName;

        // Skip empty type names
        if (string.IsNullOrEmpty(typeName))
        {
            Interlocked.Increment(ref _droppedEventCount);
            return;
        }

        // Check exclusion patterns
        if (ShouldExcludeType(typeName))
            return;

        // Update aggregated data
        var info = _typeAllocations.GetOrAdd(typeName, name => new TypeAllocationInfo
        {
            TypeName = name,
            LastSeenUtc = DateTime.UtcNow
        });

        // Thread safety updates
        // AllocationTick: Each event represents an allocation, AllocationAmount is the allocation size
        info.AddAllocationCount(1);
        info.AddTotalBytes(data.AllocationAmount);
        info.LastSeenUtc = DateTime.UtcNow;

        Interlocked.Add(ref _totalAllocationCount, 1);
        Interlocked.Add(ref _totalBytes, data.AllocationAmount);

        // Enforce a maximum number of trace types
        if (_typeAllocations.Count > _maxTrackedTypes)
        {
            PruneSmallestTypes();
        }
    }

    /// <summary>
    /// Check if the type should be excluded
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
    /// Prune smallest types to stay within limits
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
    /// capture snapshot
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
    /// Build current snapshot
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
    /// Get current snapshot
    /// </summary>
    public TypeAllocationSnapshot GetCurrentSnapshot()
    {
        return BuildSnapshot();
    }

    /// <summary>
    /// Get historical snapshot
    /// </summary>
    public IReadOnlyList<TypeAllocationSnapshot> GetHistory()
    {
        return _history.ToList();
    }

    /// <summary>
    /// Reset data
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
