using Monica.Profiling.Models;
using Monica.Core.Results;

namespace Monica.Profiling.Services;

/// <summary>
/// Memory analysis service interface
/// </summary>
public interface IMemoryAnalysisService
{
    /// <summary>
    /// Get current memory snapshot
    /// </summary>
    MemorySnapshot GetCurrentSnapshot();

    /// <summary>
    /// Get detailed GC information
    /// </summary>
    /// <param name="kind">GC type</param>
    DetailedGCInfo GetDetailedGCInfo(GCKind kind = GCKind.Any);

    /// <summary>
    /// Enforce garbage collection
    /// </summary>
    /// <param name="generation">Number of generations to collect (0, 1, 2), -1 means full collection</param>
    /// <param name="blocking">Whether to block waiting for GC to complete</param>
    /// <param name="compacting">Whether to compress</param>
    Res ForceGarbageCollection(int generation = -1, bool blocking = true, bool compacting = false);

    /// <summary>
    /// Get memory trend data
    /// </summary>
    MemoryTrendData GetMemoryTrend();

    /// <summary>
    /// Get current real-time data points
    /// </summary>
    MemoryDataPoint? GetCurrentDataPoint();

    /// <summary>
    /// Trigger generation of GC Dump file
    /// </summary>
    /// <param name="outputPath">Output path (optional, defaults to temporary directory)</param>
    Task<Res<string>> TriggerGcDumpAsync(string? outputPath = null);

    /// <summary>
    /// Subscribe to real-time indicator updates
    /// </summary>
    /// <param name="callback">callback function</param>
    /// <returns>Unsubscribe Action</returns>
    Action SubscribeToUpdates(Action<MemoryDataPoint> callback);
}