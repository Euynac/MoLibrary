using Monica.Profiling.Models;
using Monica.Tool.MoResponse;

namespace Monica.Profiling.Services;

/// <summary>
///     内存分析服务接口
/// </summary>
public interface IMemoryAnalysisService
{
    /// <summary>
    ///     获取当前内存快照
    /// </summary>
    MemorySnapshot GetCurrentSnapshot();

    /// <summary>
    ///     获取详细 GC 信息
    /// </summary>
    /// <param name="kind">GC 类型</param>
    DetailedGCInfo GetDetailedGCInfo(GCKind kind = GCKind.Any);

    /// <summary>
    ///     强制执行垃圾回收
    /// </summary>
    /// <param name="generation">要回收的代数 (0, 1, 2)，-1 表示完整回收</param>
    /// <param name="blocking">是否阻塞等待 GC 完成</param>
    /// <param name="compacting">是否进行压缩</param>
    Res ForceGarbageCollection(int generation = -1, bool blocking = true, bool compacting = false);

    /// <summary>
    ///     获取内存趋势数据
    /// </summary>
    MemoryTrendData GetMemoryTrend();

    /// <summary>
    ///     获取当前实时数据点
    /// </summary>
    MemoryDataPoint? GetCurrentDataPoint();

    /// <summary>
    ///     触发生成 GC Dump 文件
    /// </summary>
    /// <param name="outputPath">输出路径 (可选，默认为临时目录)</param>
    Task<Res<string>> TriggerGcDumpAsync(string? outputPath = null);

    /// <summary>
    ///     订阅实时指标更新
    /// </summary>
    /// <param name="callback">回调函数</param>
    /// <returns>取消订阅的 Action</returns>
    Action SubscribeToUpdates(Action<MemoryDataPoint> callback);
}