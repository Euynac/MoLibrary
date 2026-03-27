using System.ComponentModel;
using System.Diagnostics;
using System.Runtime;
using Microsoft.Extensions.Logging;
using Monica.Profiling.Models;
using Monica.Tool.Results;

namespace Monica.Profiling.Services;

/// <summary>
///     内存分析服务实现
/// </summary>
public class MemoryAnalysisService(
    ProfilingMetricsCollector metricsCollector,
    ILogger<MemoryAnalysisService> logger) : IMemoryAnalysisService
{
    /// <inheritdoc />
    public MemorySnapshot GetCurrentSnapshot()
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

    /// <inheritdoc />
    public DetailedGCInfo GetDetailedGCInfo(GCKind kind = GCKind.Any)
    {
        var gcInfo = GC.GetGCMemoryInfo(kind);
        var genInfoSpan = gcInfo.GenerationInfo;

        // 构建各代详细信息
        var genDetails = new List<GenerationDetailInfo>();
        for (var i = 0; i < genInfoSpan.Length; i++)
            genDetails.Add(new GenerationDetailInfo
            {
                Generation = i,
                SizeBeforeBytes = genInfoSpan[i].SizeBeforeBytes,
                SizeAfterBytes = genInfoSpan[i].SizeAfterBytes,
                FragmentationBeforeBytes = genInfoSpan[i].FragmentationBeforeBytes,
                FragmentationAfterBytes = genInfoSpan[i].FragmentationAfterBytes
            });

        // 计算暂停时间 (取两个暂停时间中的较大值)
        var pauseDurations = gcInfo.PauseDurations;
        var totalPause = pauseDurations.Length > 0 ? pauseDurations[0] : TimeSpan.Zero;
        if (pauseDurations.Length > 1 && pauseDurations[1] > totalPause) totalPause = pauseDurations[1];

        return new DetailedGCInfo
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

    /// <inheritdoc />
    public Res ForceGarbageCollection(int generation = -1, bool blocking = true, bool compacting = false)
    {
        try
        {
            var beforeMemory = GC.GetTotalMemory(false);

            if (generation < 0)
            {
                // 完整 GC
                if (compacting) GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;

                GC.Collect(GC.MaxGeneration, blocking ? GCCollectionMode.Forced : GCCollectionMode.Optimized, blocking,
                    compacting);
            }
            else
            {
                // 指定代数的 GC
                var targetGen = Math.Min(generation, GC.MaxGeneration);
                GC.Collect(targetGen, blocking ? GCCollectionMode.Forced : GCCollectionMode.Optimized, blocking);
            }

            if (blocking) GC.WaitForPendingFinalizers();

            var afterMemory = GC.GetTotalMemory(false);
            var freedBytes = beforeMemory - afterMemory;

            logger.LogInformation(
                "GC 完成: 代数={Generation}, 释放={FreedBytes}字节, 前={Before}MB, 后={After}MB",
                generation < 0 ? "Full" : generation.ToString(),
                freedBytes,
                beforeMemory / 1024.0 / 1024.0,
                afterMemory / 1024.0 / 1024.0);

            return Res.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "强制 GC 失败");
            return Res.Fail($"GC 执行失败: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public MemoryTrendData GetMemoryTrend()
    {
        return metricsCollector.GetTrendData();
    }

    /// <inheritdoc />
    public MemoryDataPoint? GetCurrentDataPoint()
    {
        return metricsCollector.GetCurrentDataPoint();
    }

    /// <inheritdoc />
    public async Task<Res<string>> TriggerGcDumpAsync(string? outputPath = null)
    {
        try
        {
            var process = Process.GetCurrentProcess();
            var fileName = $"gcdump_{process.ProcessName}_{DateTime.Now:yyyyMMdd_HHmmss}.gcdump";
            var dumpPath = outputPath ?? Path.Combine(Path.GetTempPath(), fileName);

            // 检查 dotnet-gcdump 是否可用
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet-gcdump",
                Arguments = $"collect -p {process.Id} -o \"{dumpPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var proc = Process.Start(startInfo);
            if (proc == null)
                return new Res<string>("无法启动 dotnet-gcdump 进程。请确保已安装: dotnet tool install -g dotnet-gcdump",
                    ResStatus.BadRequest);

            await proc.WaitForExitAsync();

            if (proc.ExitCode != 0)
            {
                var procError = await proc.StandardError.ReadToEndAsync();
                logger.LogWarning("dotnet-gcdump 执行失败: {Error}", procError);
                return new Res<string>($"GC Dump 创建失败: {procError}", ResStatus.BadRequest);
            }

            logger.LogInformation("GC Dump 已创建: {Path}", dumpPath);
            return Res.Ok(dumpPath);
        }
        catch (Exception ex) when (ex is Win32Exception)
        {
            return new Res<string>("dotnet-gcdump 工具未安装。请运行: dotnet tool install -g dotnet-gcdump",
                ResStatus.BadRequest);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "创建 GC Dump 失败");
            return new Res<string>($"创建 GC Dump 失败: {ex.Message}", ResStatus.BadRequest);
        }
    }

    /// <inheritdoc />
    public Action SubscribeToUpdates(Action<MemoryDataPoint> callback)
    {
        metricsCollector.OnMetricsUpdated += callback;
        return () => metricsCollector.OnMetricsUpdated -= callback;
    }
}