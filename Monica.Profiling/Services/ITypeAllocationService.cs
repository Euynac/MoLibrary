using Monica.Profiling.Models;
using Monica.Core.Results;

namespace Monica.Profiling.Services;

/// <summary>
/// Type allocation tracking service interface
/// </summary>
public interface ITypeAllocationService
{
    /// <summary>
    /// Whether allocation data is being collected
    /// </summary>
    bool IsCollecting { get; }

    /// <summary>
    /// Current sampling mode
    /// </summary>
    AllocationSamplingMode CurrentMode { get; }

    /// <summary>
    /// Get current allocation snapshot
    /// </summary>
    TypeAllocationSnapshot GetCurrentSnapshot();

    /// <summary>
    /// Get the top N allocation types sorted by byte count
    /// </summary>
    /// <param name="count">The number of types to return</param>
    IReadOnlyList<TypeAllocationInfo> GetTopAllocatingTypes(int count = 10);

    /// <summary>
    /// Get a list of historical snapshots
    /// </summary>
    IReadOnlyList<TypeAllocationSnapshot> GetHistorySnapshots();

    /// <summary>
    /// Start allocation tracking
    /// </summary>
    /// <param name="mode">Sampling mode</param>
    Res StartCollection(AllocationSamplingMode mode = AllocationSamplingMode.Low);

    /// <summary>
    /// Stop allocation tracking
    /// </summary>
    Res StopCollection();

    /// <summary>
    /// Reset all collected data
    /// </summary>
    void ResetData();

    /// <summary>
    /// Subscribe to snapshot updates
    /// </summary>
    /// <param name="callback">Callback function called when a new snapshot is available</param>
    /// <returns>Unsubscribe Action</returns>
    Action SubscribeToUpdates(Action<TypeAllocationSnapshot> callback);

    /// <summary>
    /// Get a heap snapshot (using ClrMD)
    /// </summary>
    /// <remarks>
    /// This operation will temporarily suspend the process, please use it with caution
    /// </remarks>
    Task<Res<TypeAllocationSnapshot>> TakeHeapSnapshotAsync();
}
