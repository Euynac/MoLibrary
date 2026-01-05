using MoLibrary.EventBus.Models;
using MudBlazor;

namespace MoLibrary.Framework.UI.UIEventBus.Models;

/// <summary>
/// 订阅变更视图模型
/// </summary>
public class SubscriptionChangeViewModel
{
    /// <summary>
    /// 变更类型
    /// </summary>
    public SubscriptionChangeType ChangeType { get; set; }

    /// <summary>
    /// 变更类型显示文本
    /// </summary>
    public string ChangeTypeDisplay => ChangeType switch
    {
        SubscriptionChangeType.Added => "添加",
        SubscriptionChangeType.Activated => "激活",
        SubscriptionChangeType.Deactivated => "停用",
        SubscriptionChangeType.Removed => "移除",
        _ => "未知"
    };

    /// <summary>
    /// 变更类型对应的颜色
    /// </summary>
    public Color ChangeTypeColor => ChangeType switch
    {
        SubscriptionChangeType.Added => Color.Success,
        SubscriptionChangeType.Activated => Color.Info,
        SubscriptionChangeType.Deactivated => Color.Warning,
        SubscriptionChangeType.Removed => Color.Error,
        _ => Color.Default
    };

    /// <summary>
    /// 变更类型对应的图标
    /// </summary>
    public string ChangeTypeIcon => ChangeType switch
    {
        SubscriptionChangeType.Added => Icons.Material.Filled.AddCircle,
        SubscriptionChangeType.Activated => Icons.Material.Filled.PlayCircle,
        SubscriptionChangeType.Deactivated => Icons.Material.Filled.PauseCircle,
        SubscriptionChangeType.Removed => Icons.Material.Filled.RemoveCircle,
        _ => Icons.Material.Filled.Circle
    };

    /// <summary>
    /// 相关订阅
    /// </summary>
    public SubscriptionViewModel Subscription { get; set; } = new();

    /// <summary>
    /// 变更时间戳
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// 时间戳显示文本（时分秒毫秒）
    /// </summary>
    public string TimestampDisplay => Timestamp.ToLocalTime().ToString("HH:mm:ss.fff");

    /// <summary>
    /// 完整时间显示文本
    /// </summary>
    public string FullTimestampDisplay => Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff");

    /// <summary>
    /// 相对时间显示（如："2分钟前"）
    /// </summary>
    public string RelativeTimeDisplay
    {
        get
        {
            var elapsed = DateTime.UtcNow - Timestamp;

            if (elapsed.TotalSeconds < 60)
                return $"{(int)elapsed.TotalSeconds}秒前";
            if (elapsed.TotalMinutes < 60)
                return $"{(int)elapsed.TotalMinutes}分钟前";
            if (elapsed.TotalHours < 24)
                return $"{(int)elapsed.TotalHours}小时前";
            if (elapsed.TotalDays < 30)
                return $"{(int)elapsed.TotalDays}天前";

            return FullTimestampDisplay;
        }
    }
}
