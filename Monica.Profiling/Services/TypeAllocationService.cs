using Microsoft.Diagnostics.Runtime;
using Microsoft.Extensions.Logging;
using Monica.Profiling.Models;
using Monica.Core.Results;

namespace Monica.Profiling.Services;

/// <summary>
/// Type allocation tracking service implementation
/// </summary>
public class TypeAllocationService(
    TypeAllocationCollector collector,
    ILogger<TypeAllocationService> logger) : ITypeAllocationService
{
    public bool IsCollecting => collector.IsCollecting;
    public AllocationSamplingMode CurrentMode => collector.CurrentMode;

    public TypeAllocationSnapshot GetCurrentSnapshot()
    {
        return collector.GetCurrentSnapshot();
    }

    public IReadOnlyList<TypeAllocationInfo> GetTopAllocatingTypes(int count = 10)
    {
        var snapshot = collector.GetCurrentSnapshot();
        return snapshot.Types.Take(count).ToList();
    }

    public IReadOnlyList<TypeAllocationSnapshot> GetHistorySnapshots()
    {
        return collector.GetHistory();
    }

    public Res StartCollection(AllocationSamplingMode mode = AllocationSamplingMode.Low)
    {
        if (collector.IsCollecting)
        {
            return Res.Fail("分配收集已在进行中，请先停止当前收集。");
        }

        try
        {
            collector.StartCollection(mode);
            logger.LogInformation("类型分配收集已启动，模式: {Mode}", mode);
            return Res.Ok();
        }
        catch (UnauthorizedAccessException)
        {
            return Res.Fail("权限不足。在 Windows 上，ETW 跟踪需要管理员权限。");
        }
        catch (PlatformNotSupportedException)
        {
            return Res.Fail("当前平台不支持此功能。ETW 分配跟踪仅在 Windows 上完全支持。");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "启动类型分配收集失败");
            return Res.Fail($"启动收集失败: {ex.Message}");
        }
    }

    public Res StopCollection()
    {
        if (!collector.IsCollecting)
        {
            return Res.Fail("当前没有正在进行的收集。");
        }

        collector.StopCollection();
        logger.LogInformation("类型分配收集已停止");
        return Res.Ok();
    }

    public void ResetData()
    {
        collector.ResetData();
        logger.LogInformation("类型分配数据已重置");
    }

    public Action SubscribeToUpdates(Action<TypeAllocationSnapshot> callback)
    {
        collector.OnSnapshotUpdated += callback;
        return () => collector.OnSnapshotUpdated -= callback;
    }

    /// <summary>
    /// Use ClrMD to get a heap snapshot
    /// </summary>
    public async Task<Res<TypeAllocationSnapshot>> TakeHeapSnapshotAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                logger.LogInformation("正在获取堆快照...");
                var startTime = DateTime.UtcNow;

                using var dataTarget = DataTarget.AttachToProcess(
                    Environment.ProcessId, false);

                var runtime = dataTarget.ClrVersions.FirstOrDefault()?.CreateRuntime();
                if (runtime == null)
                {
                    return "无法创建 CLR 运行时，可能不是托管进程。";
                }

                var heap = runtime.Heap;
                if (!heap.CanWalkHeap)
                {
                    return "无法遍历堆，GC 可能正在进行中。";
                }

                var typeStats = new Dictionary<string, (long Count, long Size)>();

                foreach (var obj in heap.EnumerateObjects())
                {
                    if (!obj.IsValid) continue;

                    var typeName = obj.Type?.Name ?? "Unknown";
                    var size = (long)obj.Size;

                    if (typeStats.TryGetValue(typeName, out var stats))
                    {
                        typeStats[typeName] = (stats.Count + 1, stats.Size + size);
                    }
                    else
                    {
                        typeStats[typeName] = (1, size);
                    }
                }

                var totalCount = typeStats.Values.Sum(v => v.Count);
                var totalBytes = typeStats.Values.Sum(v => v.Size);

                var types = typeStats
                    .Select(kvp => new TypeAllocationInfo
                    {
                        TypeName = kvp.Key,
                        AllocationCount = kvp.Value.Count,
                        TotalBytes = kvp.Value.Size,
                        AllocationPercentage = totalCount > 0 ? (double)kvp.Value.Count / totalCount * 100 : 0,
                        BytesPercentage = totalBytes > 0 ? (double)kvp.Value.Size / totalBytes * 100 : 0,
                        LastSeenUtc = DateTime.UtcNow
                    })
                    .OrderByDescending(t => t.TotalBytes)
                    .ToList();

                var duration = DateTime.UtcNow - startTime;
                logger.LogInformation("堆快照完成，发现 {TypeCount} 种类型，耗时 {Duration:F2} 秒",
                    types.Count, duration.TotalSeconds);

                return Res.Ok(new TypeAllocationSnapshot
                {
                    Timestamp = DateTime.UtcNow,
                    Duration = duration,
                    Types = types,
                    TotalAllocationCount = totalCount,
                    TotalBytes = totalBytes,
                    IsCollecting = false,
                    SamplingMode = AllocationSamplingMode.Disabled,
                    DataSource = TypeAllocationDataSource.HeapSnapshot
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取堆快照失败");
                return $"获取堆快照失败: {ex.Message}";
            }
        });
    }
}
