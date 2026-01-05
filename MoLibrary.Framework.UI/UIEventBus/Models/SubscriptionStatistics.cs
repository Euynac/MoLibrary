namespace MoLibrary.Framework.UI.UIEventBus.Models;

/// <summary>
/// 订阅统计信息
/// </summary>
public class SubscriptionStatistics
{
    #region Overall Statistics

    /// <summary>
    /// 订阅总数
    /// </summary>
    public int TotalSubscriptions { get; set; }

    /// <summary>
    /// 活跃订阅数
    /// </summary>
    public int ActiveSubscriptions { get; set; }

    /// <summary>
    /// 未激活订阅数
    /// </summary>
    public int InactiveSubscriptions { get; set; }

    /// <summary>
    /// 待激活订阅数
    /// </summary>
    public int PendingSubscriptions { get; set; }

    /// <summary>
    /// 已释放订阅数
    /// </summary>
    public int DisposedSubscriptions { get; set; }

    #endregion

    #region Scope Statistics

    /// <summary>
    /// 本地订阅数
    /// </summary>
    public int LocalSubscriptions { get; set; }

    /// <summary>
    /// 分布式订阅数
    /// </summary>
    public int DistributedSubscriptions { get; set; }

    #endregion

    #region Discovery Statistics

    /// <summary>
    /// 自动发现订阅数
    /// </summary>
    public int AutoDiscoveredCount { get; set; }

    /// <summary>
    /// 手动订阅数
    /// </summary>
    public int ManualSubscriptionCount { get; set; }

    #endregion

    #region Handler Statistics

    /// <summary>
    /// Action处理器订阅数
    /// </summary>
    public int ActionHandlerCount { get; set; }

    /// <summary>
    /// 类型处理器订阅数
    /// </summary>
    public int TypeHandlerCount { get; set; }

    #endregion

    #region Top Lists

    /// <summary>
    /// Top事件类型列表（按订阅数排序）
    /// </summary>
    public List<EventTypeCount> TopEventTypes { get; set; } = new();

    /// <summary>
    /// Top主题列表（按订阅数排序）
    /// </summary>
    public List<TopicCount> TopTopics { get; set; } = new();

    #endregion

    #region Computed Properties

    /// <summary>
    /// 活跃率（百分比）
    /// </summary>
    public double ActiveRate => TotalSubscriptions > 0
        ? (double)ActiveSubscriptions / TotalSubscriptions * 100
        : 0;

    /// <summary>
    /// 活跃率显示文本
    /// </summary>
    public string ActiveRateDisplay => $"{ActiveRate:F1}%";

    /// <summary>
    /// 自动发现率（百分比）
    /// </summary>
    public double AutoDiscoveredRate => TotalSubscriptions > 0
        ? (double)AutoDiscoveredCount / TotalSubscriptions * 100
        : 0;

    /// <summary>
    /// 自动发现率显示文本
    /// </summary>
    public string AutoDiscoveredRateDisplay => $"{AutoDiscoveredRate:F1}%";

    #endregion
}

/// <summary>
/// 事件类型统计
/// </summary>
public class EventTypeCount
{
    /// <summary>
    /// 事件类型名称
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// 事件类型简短名称
    /// </summary>
    public string EventTypeShortName { get; set; } = string.Empty;

    /// <summary>
    /// 订阅数量
    /// </summary>
    public int Count { get; set; }
}

/// <summary>
/// 主题统计
/// </summary>
public class TopicCount
{
    /// <summary>
    /// 主题名称
    /// </summary>
    public string TopicName { get; set; } = string.Empty;

    /// <summary>
    /// 订阅数量
    /// </summary>
    public int Count { get; set; }
}
