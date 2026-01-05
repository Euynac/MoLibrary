using MoLibrary.EventBus.Models;

namespace MoLibrary.Framework.UI.UIEventBus.Models;

/// <summary>
/// 订阅过滤条件
/// </summary>
public class SubscriptionFilter
{
    /// <summary>
    /// 按状态过滤
    /// </summary>
    public SubscriptionState? State { get; set; }

    /// <summary>
    /// 按范围过滤
    /// </summary>
    public SubscriptionScope? Scope { get; set; }

    /// <summary>
    /// 按服务键过滤
    /// </summary>
    public string? ServiceKey { get; set; }

    /// <summary>
    /// 按是否自动发现过滤
    /// </summary>
    public bool? IsAutoDiscovered { get; set; }

    /// <summary>
    /// 搜索文本（在事件类型或主题名称中搜索）
    /// </summary>
    public string? SearchText { get; set; }

    /// <summary>
    /// 是否有任何过滤条件
    /// </summary>
    public bool HasAnyFilter =>
        State.HasValue ||
        Scope.HasValue ||
        !string.IsNullOrWhiteSpace(ServiceKey) ||
        IsAutoDiscovered.HasValue ||
        !string.IsNullOrWhiteSpace(SearchText);

    /// <summary>
    /// 清空所有过滤条件
    /// </summary>
    public void Clear()
    {
        State = null;
        Scope = null;
        ServiceKey = null;
        IsAutoDiscovered = null;
        SearchText = null;
    }

    /// <summary>
    /// 创建副本
    /// </summary>
    public SubscriptionFilter Clone()
    {
        return new SubscriptionFilter
        {
            State = State,
            Scope = Scope,
            ServiceKey = ServiceKey,
            IsAutoDiscovered = IsAutoDiscovered,
            SearchText = SearchText
        };
    }
}
