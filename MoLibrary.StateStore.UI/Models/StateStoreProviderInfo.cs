using MoLibrary.StateStore.Providers;

namespace MoLibrary.StateStore.UI.Models;

/// <summary>
/// 表示已注册的 StateStore Provider 信息
/// </summary>
public class StateStoreProviderInfo
{
    /// <summary>
    /// 服务键 (null 表示非 Keyed 的默认 Provider)
    /// </summary>
    public string? ServiceKey { get; init; }

    /// <summary>
    /// 显示名称
    /// </summary>
    public string DisplayName => ServiceKey ?? "默认";

    /// <summary>
    /// Provider 类型
    /// </summary>
    public EStateStoreProviderType ProviderType { get; init; }

    /// <summary>
    /// Provider 能力
    /// </summary>
    public EStateStoreCapabilities Capabilities { get; init; }

    /// <summary>
    /// 是否为分布式 StateStore
    /// </summary>
    public bool IsDistributed { get; init; }

    /// <summary>
    /// 是否为 IMoStateStore 的默认实现（用户直接注入 IMoStateStore 时获得的实例）
    /// </summary>
    public bool IsDefaultIMoStateStore { get; init; }

    /// <summary>
    /// Provider Option 的类型
    /// </summary>
    public Type? OptionType { get; init; }

    /// <summary>
    /// Provider Option 的实例
    /// </summary>
    public object? OptionInstance { get; init; }

    /// <summary>
    /// 实现类型名称
    /// </summary>
    public string ImplementationType { get; init; } = "";

    /// <summary>
    /// 检查 Provider 是否支持 Key 扫描
    /// </summary>
    public bool SupportsKeyScanning => Capabilities.HasFlag(EStateStoreCapabilities.KeyScanning);

    /// <summary>
    /// 获取 Provider 类型的显示名称
    /// </summary>
    public string ProviderTypeName => ProviderType switch
    {
        EStateStoreProviderType.Memory => "内存缓存",
        EStateStoreProviderType.Redis => "Redis",
        EStateStoreProviderType.Dapr => "Dapr",
        _ => "未知"
    };
}
