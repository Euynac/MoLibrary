using Monica.Profiling.Models;
using Monica.Tool.Results;

namespace Monica.Profiling.Services;

/// <summary>
///     类型分配跟踪服务接口
/// </summary>
public interface ITypeAllocationService
{
    /// <summary>
    ///     是否正在收集分配数据
    /// </summary>
    bool IsCollecting { get; }

    /// <summary>
    ///     当前采样模式
    /// </summary>
    AllocationSamplingMode CurrentMode { get; }

    /// <summary>
    ///     获取当前分配快照
    /// </summary>
    TypeAllocationSnapshot GetCurrentSnapshot();

    /// <summary>
    ///     获取按字节数排序的前 N 个分配类型
    /// </summary>
    /// <param name="count">要返回的类型数量</param>
    IReadOnlyList<TypeAllocationInfo> GetTopAllocatingTypes(int count = 10);

    /// <summary>
    ///     获取历史快照列表
    /// </summary>
    IReadOnlyList<TypeAllocationSnapshot> GetHistorySnapshots();

    /// <summary>
    ///     开始分配跟踪
    /// </summary>
    /// <param name="mode">采样模式</param>
    Res StartCollection(AllocationSamplingMode mode = AllocationSamplingMode.Low);

    /// <summary>
    ///     停止分配跟踪
    /// </summary>
    Res StopCollection();

    /// <summary>
    ///     重置所有收集的数据
    /// </summary>
    void ResetData();

    /// <summary>
    ///     订阅快照更新
    /// </summary>
    /// <param name="callback">新快照可用时调用的回调函数</param>
    /// <returns>取消订阅的 Action</returns>
    Action SubscribeToUpdates(Action<TypeAllocationSnapshot> callback);

    /// <summary>
    ///     获取堆快照 (使用 ClrMD)
    /// </summary>
    /// <remarks>
    ///     此操作会短暂暂停进程，请谨慎使用
    /// </remarks>
    Task<Res<TypeAllocationSnapshot>> TakeHeapSnapshotAsync();
}
