using Monica.EventBus.Models;
using MudBlazor;

namespace Monica.Framework.UI.UIEventBus.Models;

/// <summary>
/// UI friendly subscription view model
/// </summary>
public class SubscriptionViewModel
{
    #region Identity

    /// <summary>
    /// Subscription ID
    /// </summary>
    public string SubscriptionId { get; set; } = string.Empty;

    /// <summary>
    /// Event type full name
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Event type short name (for display)
    /// </summary>
    public string EventTypeShortName { get; set; } = string.Empty;

    /// <summary>
    /// Topic name
    /// </summary>
    public string TopicName { get; set; } = string.Empty;

    /// <summary>
    /// Service key (for Keyed EventBus)
    /// </summary>
    public string? ServiceKey { get; set; }

    #endregion

    #region Handler Information

    /// <summary>
    /// Processor type full name
    /// </summary>
    public string? HandlerType { get; set; }

    /// <summary>
    /// Processor type short name (for display)
    /// </summary>
    public string? HandlerTypeShortName { get; set; }

    /// <summary>
    /// Processor factory type name
    /// </summary>
    public string HandlerFactoryType { get; set; } = string.Empty;

    /// <summary>
    /// Whether it is an Action processor
    /// </summary>
    public bool IsActionHandler => HandlerType == null;

    #endregion

    #region Action Handler Metadata

    /// <summary>
    /// Action processor method name
    /// </summary>
    public string? ActionMethodName { get; set; }

    /// <summary>
    /// Declared type of action handler
    /// </summary>
    public string? ActionDeclaringType { get; set; }

    /// <summary>
    /// Action handler method signature
    /// </summary>
    public string? ActionMethodSignature { get; set; }

    /// <summary>
    /// Whether the method of the Action processor is a static method
    /// </summary>
    public bool? ActionIsStatic { get; set; }

    /// <summary>
    /// Get the full description of the Action handler (used in tooltips)
    /// </summary>
    public string ActionHandlerTooltip
    {
        get
        {
            if (!IsActionHandler) return string.Empty;

            var parts = new List<string>();

            if (!string.IsNullOrEmpty(ActionDeclaringType))
            {
                parts.Add($"声明类型: {ActionDeclaringType}");
            }

            if (!string.IsNullOrEmpty(ActionMethodName))
            {
                parts.Add($"方法名称: {ActionMethodName}");
            }

            if (!string.IsNullOrEmpty(ActionMethodSignature))
            {
                parts.Add($"方法签名: {ActionMethodSignature}");
            }

            if (ActionIsStatic.HasValue)
            {
                parts.Add($"静态方法: {(ActionIsStatic.Value ? "是" : "否")}");
            }

            return parts.Count > 0 ? string.Join("\n", parts) : "Action处理器";
        }
    }

    #endregion

    #region Scope & State

    /// <summary>
    /// Subscription scope
    /// </summary>
    public EventSubscriptionScope Scope { get; set; }

    /// <summary>
    /// Subscription range display text
    /// </summary>
    public string ScopeDisplay => Scope == EventSubscriptionScope.Local ? "本地" : "分布式";

    /// <summary>
    /// Subscription status
    /// </summary>
    public EventSubscriptionState State { get; set; }

    /// <summary>
    /// Subscription status display text
    /// </summary>
    public string StateDisplay => State switch
    {
        EventSubscriptionState.Pending => "待激活",
        EventSubscriptionState.Active => "活跃",
        EventSubscriptionState.Inactive => "未激活",
        EventSubscriptionState.Disposed => "已释放",
        _ => "未知"
    };

    /// <summary>
    /// The color corresponding to the subscription status
    /// </summary>
    public Color StateColor => State switch
    {
        EventSubscriptionState.Active => Color.Success,
        EventSubscriptionState.Pending => Color.Warning,
        EventSubscriptionState.Inactive => Color.Default,
        EventSubscriptionState.Disposed => Color.Error,
        _ => Color.Default
    };

    /// <summary>
    /// The color corresponding to the subscription range
    /// </summary>
    public Color ScopeColor => Scope == EventSubscriptionScope.Local ? Color.Info : Color.Secondary;

    #endregion

    #region Lifecycle

    /// <summary>
    /// creation time
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// activation time
    /// </summary>
    public DateTime? ActivatedAt { get; set; }

    /// <summary>
    /// deactivation time
    /// </summary>
    public DateTime? DeactivatedAt { get; set; }

    /// <summary>
    /// Whether to automatically discover
    /// </summary>
    public bool IsAutoDiscovered { get; set; }

    #endregion

    #region Metadata

    /// <summary>
    /// metadata
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    #endregion

    #region Display Properties

    /// <summary>
    /// Creation time display text
    /// </summary>
    public string CreatedAtDisplay => CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    /// <summary>
    /// Activation time display text
    /// </summary>
    public string? ActivatedAtDisplay => ActivatedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    /// <summary>
    /// Disable time display text
    /// </summary>
    public string? DeactivatedAtDisplay => DeactivatedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    /// <summary>
    /// duration display text
    /// </summary>
    public string DurationDisplay
    {
        get
        {
            if (State == EventSubscriptionState.Active && ActivatedAt.HasValue)
            {
                var duration = DateTime.UtcNow - ActivatedAt.Value;
                return FormatDuration(duration);
            }
            else if (State == EventSubscriptionState.Inactive && DeactivatedAt.HasValue && ActivatedAt.HasValue)
            {
                var duration = DeactivatedAt.Value - ActivatedAt.Value;
                return FormatDuration(duration);
            }
            else if (State == EventSubscriptionState.Disposed && CreatedAt != default)
            {
                var endTime = DeactivatedAt ?? ActivatedAt ?? DateTime.UtcNow;
                var duration = endTime - CreatedAt;
                return FormatDuration(duration);
            }
            return "-";
        }
    }

    /// <summary>
    /// Processor displays text
    /// </summary>
    public string HandlerDisplay
    {
        get
        {
            if (IsActionHandler)
            {
                // If there is a method name, display the method name
                if (!string.IsNullOrEmpty(ActionMethodName))
                {
                    return $"Action: {ActionMethodName}";
                }
                return "Action处理器";
            }
            return HandlerTypeShortName ?? "未知";
        }
    }

    #endregion

    #region Helper Methods

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalDays >= 1)
            return $"{(int)duration.TotalDays}天 {duration.Hours}小时";
        if (duration.TotalHours >= 1)
            return $"{(int)duration.TotalHours}小时 {duration.Minutes}分钟";
        if (duration.TotalMinutes >= 1)
            return $"{(int)duration.TotalMinutes}分钟 {duration.Seconds}秒";
        return $"{(int)duration.TotalSeconds}秒";
    }

    #endregion
}
