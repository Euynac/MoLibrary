using Monica.EventBus.Models;
using MudBlazor;

namespace Monica.Framework.UI.UIEventBus.Models;

/// <summary>
/// Subscribe to change view model
/// </summary>
public class SubscriptionChangeViewModel
{
    /// <summary>
    /// Change type
    /// </summary>
    public EventSubscriptionChangeType ChangeType { get; set; }

    /// <summary>
    /// Change type display text
    /// </summary>
    public string ChangeTypeDisplay => ChangeType switch
    {
        EventSubscriptionChangeType.Added => "添加",
        EventSubscriptionChangeType.Activated => "激活",
        EventSubscriptionChangeType.Deactivated => "停用",
        EventSubscriptionChangeType.Removed => "移除",
        _ => "未知"
    };

    /// <summary>
    /// The color corresponding to the change type
    /// </summary>
    public Color ChangeTypeColor => ChangeType switch
    {
        EventSubscriptionChangeType.Added => Color.Success,
        EventSubscriptionChangeType.Activated => Color.Info,
        EventSubscriptionChangeType.Deactivated => Color.Warning,
        EventSubscriptionChangeType.Removed => Color.Error,
        _ => Color.Default
    };

    /// <summary>
    /// Icon corresponding to the change type
    /// </summary>
    public string ChangeTypeIcon => ChangeType switch
    {
        EventSubscriptionChangeType.Added => Icons.Material.Filled.AddCircle,
        EventSubscriptionChangeType.Activated => Icons.Material.Filled.PlayCircle,
        EventSubscriptionChangeType.Deactivated => Icons.Material.Filled.PauseCircle,
        EventSubscriptionChangeType.Removed => Icons.Material.Filled.RemoveCircle,
        _ => Icons.Material.Filled.Circle
    };

    /// <summary>
    /// Related subscriptions
    /// </summary>
    public SubscriptionViewModel Subscription { get; set; } = new();

    /// <summary>
    /// Change timestamp
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Timestamp display text (hours minutes seconds milliseconds)
    /// </summary>
    public string TimestampDisplay => Timestamp.ToLocalTime().ToString("HH:mm:ss.fff");

    /// <summary>
    /// Full time display text
    /// </summary>
    public string FullTimestampDisplay => Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff");

    /// <summary>
    /// Relative time display (eg: "2 minutes ago")
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
