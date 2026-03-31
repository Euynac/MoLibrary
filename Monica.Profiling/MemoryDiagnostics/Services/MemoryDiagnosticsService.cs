using System.Runtime;
using Microsoft.Extensions.Logging;
using Monica.Profiling.MemoryDiagnostics.Models;
using Monica.Profiling.MemoryDiagnostics.Providers.DotNetTools;

namespace Monica.Profiling.MemoryDiagnostics.Services;

/// <summary>
/// Captures memory diagnostics and performs GC-oriented operations using standard .NET exceptions for failure.
/// </summary>
internal sealed class MemoryDiagnosticsService(
    DotNetGcDumpProvider gcDumpProvider,
    ILogger<MemoryDiagnosticsService> logger)
{
    public MemorySnapshot CaptureSnapshot()
    {
        var gcInfo = GC.GetGCMemoryInfo();

        return new MemorySnapshot
        {
            Timestamp = DateTime.UtcNow,
            WorkingSetBytes = Environment.WorkingSet,
            TotalManagedMemory = GC.GetTotalMemory(false),
            GcHeapSizeBytes = gcInfo.HeapSizeBytes,
            CommittedBytes = gcInfo.TotalCommittedBytes,
            Gen0Collections = GC.CollectionCount(0),
            Gen1Collections = GC.CollectionCount(1),
            Gen2Collections = GC.CollectionCount(2),
            IsServerGC = GCSettings.IsServerGC,
            LatencyMode = GCSettings.LatencyMode
        };
    }

    public GcDetails CaptureGcDetails(GCKind kind = GCKind.Any)
    {
        var gcInfo = GC.GetGCMemoryInfo(kind);
        var genInfoSpan = gcInfo.GenerationInfo;

        // Build generation details
        var genDetails = new List<GcGenerationDetails>();
        for (var i = 0; i < genInfoSpan.Length; i++)
            genDetails.Add(new GcGenerationDetails
            {
                Generation = i,
                SizeBeforeBytes = genInfoSpan[i].SizeBeforeBytes,
                SizeAfterBytes = genInfoSpan[i].SizeAfterBytes,
                FragmentationBeforeBytes = genInfoSpan[i].FragmentationBeforeBytes,
                FragmentationAfterBytes = genInfoSpan[i].FragmentationAfterBytes
            });

        // Calculate the pause time (take the larger of the two pause times)
        var pauseDurations = gcInfo.PauseDurations;
        var totalPause = pauseDurations.Length > 0 ? pauseDurations[0] : TimeSpan.Zero;
        if (pauseDurations.Length > 1 && pauseDurations[1] > totalPause) totalPause = pauseDurations[1];

        return new GcDetails
        {
            GCIndex = gcInfo.Index,
            Generation = gcInfo.Generation,
            WasCompacting = gcInfo.Compacted,
            WasConcurrent = gcInfo.Concurrent,
            HeapSizeBytes = gcInfo.HeapSizeBytes,
            FragmentedBytes = gcInfo.FragmentedBytes,
            CommittedBytes = gcInfo.TotalCommittedBytes,
            PromotedBytes = gcInfo.PromotedBytes,
            PinnedObjectsCount = gcInfo.PinnedObjectsCount,
            FinalizationPendingCount = gcInfo.FinalizationPendingCount,
            PauseDuration = totalPause,
            PauseTimePercentage = gcInfo.PauseTimePercentage,
            HighMemoryLoadThresholdBytes = gcInfo.HighMemoryLoadThresholdBytes,
            MemoryLoadBytes = gcInfo.MemoryLoadBytes,
            TotalAvailableMemoryBytes = gcInfo.TotalAvailableMemoryBytes,
            GenerationDetails = genDetails.ToArray()
        };
    }

    public void ForceGarbageCollection(int generation = -1, bool blocking = true, bool compacting = false)
    {
        var beforeMemory = GC.GetTotalMemory(false);

        if (generation < 0)
        {
            if (compacting)
            {
                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            }

            GC.Collect(
                GC.MaxGeneration,
                blocking ? GCCollectionMode.Forced : GCCollectionMode.Optimized,
                blocking,
                compacting);
        }
        else
        {
            var targetGeneration = Math.Min(generation, GC.MaxGeneration);
            GC.Collect(targetGeneration, blocking ? GCCollectionMode.Forced : GCCollectionMode.Optimized, blocking);
        }

        if (blocking)
        {
            GC.WaitForPendingFinalizers();
        }

        var afterMemory = GC.GetTotalMemory(false);
        var freedBytes = beforeMemory - afterMemory;

        logger.LogInformation(
            "GC completed. Generation={Generation}, FreedBytes={FreedBytes}, BeforeMb={BeforeMb}, AfterMb={AfterMb}",
            generation < 0 ? "Full" : generation.ToString(),
            freedBytes,
            beforeMemory / 1024.0 / 1024.0,
            afterMemory / 1024.0 / 1024.0);
    }

    public Task<string> CreateGcDumpAsync(string? outputPath = null)
    {
        return gcDumpProvider.CollectAsync(outputPath);
    }
}
