using Microsoft.Diagnostics.Runtime;
using Microsoft.Extensions.Logging;
using Monica.Profiling.TypeAllocation.Models;

namespace Monica.Profiling.TypeAllocation.Providers.ClrMd;

internal sealed class ClrMdHeapSnapshotProvider(ILogger<ClrMdHeapSnapshotProvider> logger)
{
    public async Task<TypeAllocationSnapshot> TakeHeapSnapshotAsync()
    {
        return await Task.Run(() =>
        {
            logger.LogInformation("Capturing CLR heap snapshot.");
            var startTime = DateTime.UtcNow;

            using var dataTarget = DataTarget.AttachToProcess(Environment.ProcessId, false);

            var runtime = dataTarget.ClrVersions.FirstOrDefault()?.CreateRuntime();
            if (runtime == null)
            {
                throw new InvalidOperationException("Unable to create the CLR runtime. The current process may not be managed.");
            }

            var heap = runtime.Heap;
            if (!heap.CanWalkHeap)
            {
                throw new InvalidOperationException("Unable to walk the CLR heap. A GC may already be running.");
            }

            var typeStats = new Dictionary<string, (long Count, long Size)>();

            foreach (var obj in heap.EnumerateObjects())
            {
                if (!obj.IsValid)
                {
                    continue;
                }

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
            var snapshotTimestamp = DateTime.UtcNow;

            var types = typeStats
                .Select(kvp => new TypeAllocationInfo
                {
                    TypeName = kvp.Key,
                    AllocationCount = kvp.Value.Count,
                    TotalBytes = kvp.Value.Size,
                    AllocationPercentage = totalCount > 0 ? (double)kvp.Value.Count / totalCount * 100 : 0,
                    BytesPercentage = totalBytes > 0 ? (double)kvp.Value.Size / totalBytes * 100 : 0,
                    LastSeenUtc = snapshotTimestamp
                })
                .OrderByDescending(t => t.TotalBytes)
                .ToList();

            var duration = DateTime.UtcNow - startTime;
            logger.LogInformation(
                "CLR heap snapshot captured. TypeCount={TypeCount}, DurationSeconds={DurationSeconds:F2}",
                types.Count,
                duration.TotalSeconds);

            return new TypeAllocationSnapshot
            {
                Timestamp = snapshotTimestamp,
                Duration = duration,
                Types = types,
                TotalAllocationCount = totalCount,
                TotalBytes = totalBytes,
                IsCollecting = false,
                SamplingMode = AllocationSamplingMode.Disabled,
                DataSource = TypeAllocationDataSource.HeapSnapshot
            };
        });
    }
}
