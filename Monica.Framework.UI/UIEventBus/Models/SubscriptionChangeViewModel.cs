using Monica.Core.Localization.Services;
using Monica.EventBus.Models;
using Monica.Framework.UI.Localization;
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
    public string ChangeTypeDisplay => LocalizationManager.For<EventBusResource>().GetSubscriptionChangeTypeText(ChangeType);

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
            return LocalizationManager.For<EventBusResource>().FormatEventBusRelativeTime(Timestamp);
        }
    }
}
