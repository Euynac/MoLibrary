using Microsoft.Extensions.Logging;
using Monica.Profiling.TypeAllocation.Models;
using Monica.Profiling.TypeAllocation.Providers.ClrMd;
using Monica.Profiling.TypeAllocation.Providers.TraceEvent;

namespace Monica.Profiling.TypeAllocation.Services;

/// <summary>
/// Orchestrates live allocation tracking and heap snapshots using standard .NET exceptions for failure.
/// </summary>
internal sealed class TypeAllocationTrackingService(
    TraceEventTypeAllocationProvider provider,
    ClrMdHeapSnapshotProvider heapSnapshotProvider,
    ILogger<TypeAllocationTrackingService> logger)
{
    public bool IsCollecting => provider.IsCollecting;

    public AllocationSamplingMode CurrentMode => provider.CurrentMode;

    public TypeAllocationSnapshot GetCurrentSnapshot()
    {
        return provider.GetCurrentSnapshot();
    }

    public IReadOnlyList<TypeAllocationInfo> GetTopAllocatingTypes(int count = 10)
    {
        var snapshot = provider.GetCurrentSnapshot();
        return snapshot.Types.Take(count).ToList();
    }

    public IReadOnlyList<TypeAllocationSnapshot> GetHistorySnapshots()
    {
        return provider.GetHistory();
    }

    public void StartCollection(AllocationSamplingMode mode = AllocationSamplingMode.Low)
    {
        if (provider.IsCollecting)
        {
            throw new InvalidOperationException("Type allocation collection is already running.");
        }

        provider.StartCollection(mode);
        logger.LogInformation("Type allocation collection started with mode {Mode}.", mode);
    }

    public void StopCollection()
    {
        if (!provider.IsCollecting)
        {
            throw new InvalidOperationException("Type allocation collection is not running.");
        }

        provider.StopCollection();
        logger.LogInformation("Type allocation collection stopped.");
    }

    public void ResetData()
    {
        provider.ResetData();
        logger.LogInformation("Type allocation data reset.");
    }

    public Action SubscribeToUpdates(Action<TypeAllocationSnapshot> callback)
    {
        provider.OnSnapshotUpdated += callback;
        return () => provider.OnSnapshotUpdated -= callback;
    }

    public Task<TypeAllocationSnapshot> TakeHeapSnapshotAsync()
    {
        return heapSnapshotProvider.TakeHeapSnapshotAsync();
    }
}
