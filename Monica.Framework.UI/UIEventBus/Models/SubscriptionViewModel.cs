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

    #endregion

    #region Scope & State

    /// <summary>
    /// Subscription scope
    /// </summary>
    public EventSubscriptionScope Scope { get; set; }

    /// <summary>
    /// Subscription status
    /// </summary>
    public EventSubscriptionState State { get; set; }

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
    /// Calculates the duration represented by the current subscription lifecycle state.
    /// </summary>
    /// <returns>
    /// The active or completed duration, or <see langword="null"/> when the lifecycle has not produced a duration yet.
    /// </returns>
    public TimeSpan? GetDuration()
    {
        if (State == EventSubscriptionState.Active && ActivatedAt.HasValue)
        {
            return DateTime.UtcNow - ActivatedAt.Value;
        }

        if (State == EventSubscriptionState.Inactive && DeactivatedAt.HasValue && ActivatedAt.HasValue)
        {
            return DeactivatedAt.Value - ActivatedAt.Value;
        }

        if (State == EventSubscriptionState.Disposed && CreatedAt != default)
        {
            var endTime = DeactivatedAt ?? ActivatedAt ?? DateTime.UtcNow;
            return endTime - CreatedAt;
        }

        return null;
    }

    #endregion
}
