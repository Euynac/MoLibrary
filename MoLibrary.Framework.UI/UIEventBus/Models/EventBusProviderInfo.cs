using MoLibrary.EventBus.Providers;

namespace MoLibrary.Framework.UI.UIEventBus.Models;

/// <summary>
/// 表示已注册的 EventBus Provider 信息
/// </summary>
public class EventBusProviderInfo
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
    public EEventBusProviderType ProviderType { get; init; }

    /// <summary>
    /// Provider 能力
    /// </summary>
    public EEventBusCapabilities Capabilities { get; init; }

    /// <summary>
    /// 是否为分布式 EventBus
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
    /// 订阅总数
    /// </summary>
    public int SubscriptionCount { get; set; }

    /// <summary>
    /// 活跃订阅数
    /// </summary>
    public int ActiveSubscriptionCount { get; set; }

    /// <summary>
    /// 检查 Provider 是否支持批量发布
    /// </summary>
    public bool SupportsBulkPublish => Capabilities.HasFlag(EEventBusCapabilities.BulkPublish);

    /// <summary>
    /// 检查 Provider 是否支持流式订阅
    /// </summary>
    public bool SupportsStreaming => Capabilities.HasFlag(EEventBusCapabilities.Streaming);

    /// <summary>
    /// 检查 Provider 是否支持死信队列
    /// </summary>
    public bool SupportsDeadLetterQueue => Capabilities.HasFlag(EEventBusCapabilities.DeadLetterQueue);

    /// <summary>
    /// 获取 Provider 类型的显示名称
    /// </summary>
    public string ProviderTypeName => ProviderType switch
    {
        EEventBusProviderType.Local => "本地",
        EEventBusProviderType.Dapr => "Dapr",
        _ => "未知"
    };

    /// <summary>
    /// 获取唯一标识 (用于比较和选择)
    /// </summary>
    public string UniqueId => $"{ProviderType}:{ServiceKey ?? "default"}:{(IsDistributed ? "dist" : "local")}";
}
