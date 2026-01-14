namespace MoLibrary.StateStore.UI.Models;

/// <summary>
/// StateStore Provider 类型枚举
/// </summary>
public enum EStateStoreProviderType
{
    /// <summary>
    /// 内存缓存 Provider
    /// </summary>
    Memory,

    /// <summary>
    /// Redis Provider
    /// </summary>
    Redis,

    /// <summary>
    /// Dapr StateStore Provider
    /// </summary>
    Dapr,

    /// <summary>
    /// 未知类型
    /// </summary>
    Unknown
}

/// <summary>
/// StateStore Provider 能力标志
/// </summary>
[Flags]
public enum EStateStoreCapabilities
{
    /// <summary>
    /// 无特殊能力
    /// </summary>
    None = 0,

    /// <summary>
    /// 支持 Key 扫描 (ScanKeysAsync)
    /// </summary>
    KeyScanning = 1 << 0,

    /// <summary>
    /// 支持原始字符串检索
    /// </summary>
    RawStringRetrieval = 1 << 1,

    /// <summary>
    /// 支持查询状态 (QueryStateAsync)
    /// </summary>
    QueryState = 1 << 2,

    /// <summary>
    /// 支持批量操作
    /// </summary>
    BulkOperations = 1 << 3
}

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
