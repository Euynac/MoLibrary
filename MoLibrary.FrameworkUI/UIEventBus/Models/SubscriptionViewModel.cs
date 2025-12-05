using MoLibrary.EventBus.Models;
using MudBlazor;

namespace MoLibrary.FrameworkUI.UIEventBus.Models;

/// <summary>
/// UI友好的订阅视图模型
/// </summary>
public class SubscriptionViewModel
{
    #region Identity

    /// <summary>
    /// 订阅ID
    /// </summary>
    public string SubscriptionId { get; set; } = string.Empty;

    /// <summary>
    /// 事件类型完整名称
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// 事件类型简短名称（用于显示）
    /// </summary>
    public string EventTypeShortName { get; set; } = string.Empty;

    /// <summary>
    /// 主题名称
    /// </summary>
    public string TopicName { get; set; } = string.Empty;

    /// <summary>
    /// 服务键（用于Keyed EventBus）
    /// </summary>
    public string? ServiceKey { get; set; }

    #endregion

    #region Handler Information

    /// <summary>
    /// 处理器类型完整名称
    /// </summary>
    public string? HandlerType { get; set; }

    /// <summary>
    /// 处理器类型简短名称（用于显示）
    /// </summary>
    public string? HandlerTypeShortName { get; set; }

    /// <summary>
    /// 处理器工厂类型名称
    /// </summary>
    public string HandlerFactoryType { get; set; } = string.Empty;

    /// <summary>
    /// 是否为Action处理器
    /// </summary>
    public bool IsActionHandler => HandlerType == null;

    #endregion

    #region Action Handler Metadata

    /// <summary>
    /// Action处理器的方法名称
    /// </summary>
    public string? ActionMethodName { get; set; }

    /// <summary>
    /// Action处理器的声明类型
    /// </summary>
    public string? ActionDeclaringType { get; set; }

    /// <summary>
    /// Action处理器的方法签名
    /// </summary>
    public string? ActionMethodSignature { get; set; }

    /// <summary>
    /// Action处理器的方法是否为静态方法
    /// </summary>
    public bool? ActionIsStatic { get; set; }

    /// <summary>
    /// 获取Action处理器的完整描述（用于工具提示）
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
    /// 订阅范围
    /// </summary>
    public SubscriptionScope Scope { get; set; }

    /// <summary>
    /// 订阅范围显示文本
    /// </summary>
    public string ScopeDisplay => Scope == SubscriptionScope.Local ? "本地" : "分布式";

    /// <summary>
    /// 订阅状态
    /// </summary>
    public SubscriptionState State { get; set; }

    /// <summary>
    /// 订阅状态显示文本
    /// </summary>
    public string StateDisplay => State switch
    {
        SubscriptionState.Pending => "待激活",
        SubscriptionState.Active => "活跃",
        SubscriptionState.Inactive => "未激活",
        SubscriptionState.Disposed => "已释放",
        _ => "未知"
    };

    /// <summary>
    /// 订阅状态对应的颜色
    /// </summary>
    public Color StateColor => State switch
    {
        SubscriptionState.Active => Color.Success,
        SubscriptionState.Pending => Color.Warning,
        SubscriptionState.Inactive => Color.Default,
        SubscriptionState.Disposed => Color.Error,
        _ => Color.Default
    };

    /// <summary>
    /// 订阅范围对应的颜色
    /// </summary>
    public Color ScopeColor => Scope == SubscriptionScope.Local ? Color.Info : Color.Secondary;

    #endregion

    #region Lifecycle

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 激活时间
    /// </summary>
    public DateTime? ActivatedAt { get; set; }

    /// <summary>
    /// 停用时间
    /// </summary>
    public DateTime? DeactivatedAt { get; set; }

    /// <summary>
    /// 是否自动发现
    /// </summary>
    public bool IsAutoDiscovered { get; set; }

    #endregion

    #region Metadata

    /// <summary>
    /// 元数据
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    #endregion

    #region Display Properties

    /// <summary>
    /// 创建时间显示文本
    /// </summary>
    public string CreatedAtDisplay => CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    /// <summary>
    /// 激活时间显示文本
    /// </summary>
    public string? ActivatedAtDisplay => ActivatedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    /// <summary>
    /// 停用时间显示文本
    /// </summary>
    public string? DeactivatedAtDisplay => DeactivatedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    /// <summary>
    /// 持续时间显示文本
    /// </summary>
    public string DurationDisplay
    {
        get
        {
            if (State == SubscriptionState.Active && ActivatedAt.HasValue)
            {
                var duration = DateTime.UtcNow - ActivatedAt.Value;
                return FormatDuration(duration);
            }
            else if (State == SubscriptionState.Inactive && DeactivatedAt.HasValue && ActivatedAt.HasValue)
            {
                var duration = DeactivatedAt.Value - ActivatedAt.Value;
                return FormatDuration(duration);
            }
            else if (State == SubscriptionState.Disposed && CreatedAt != default)
            {
                var endTime = DeactivatedAt ?? ActivatedAt ?? DateTime.UtcNow;
                var duration = endTime - CreatedAt;
                return FormatDuration(duration);
            }
            return "-";
        }
    }

    /// <summary>
    /// 处理器显示文本
    /// </summary>
    public string HandlerDisplay
    {
        get
        {
            if (IsActionHandler)
            {
                // 如果有方法名，显示方法名
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
