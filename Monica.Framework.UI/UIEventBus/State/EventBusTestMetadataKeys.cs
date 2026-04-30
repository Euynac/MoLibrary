using Monica.EventBus.Abstractions;
using Monica.EventBus.Models;

namespace Monica.Framework.UI.UIEventBus.State;

/// <summary>
/// Metadata keys used to identify temporary EventBus UI test subscriptions.
/// </summary>
internal static class EventBusTestMetadataKeys
{
    /// <summary>
    /// Marks a subscription as an EventBus UI test listener that should be hidden from normal monitoring views.
    /// </summary>
    public const string IsTestListener = "Monica.EventBusUI.TestListener";

    /// <summary>
    /// Stores the source subscription id that owns the temporary test listener.
    /// </summary>
    public const string SourceSubscriptionId = "Monica.EventBusUI.SourceSubscriptionId";

    /// <summary>
    /// Creates metadata for a temporary listener subscription.
    /// </summary>
    /// <param name="sourceSubscriptionId">The subscription id selected by the UI user.</param>
    /// <returns>Metadata applied to the temporary listener subscription.</returns>
    public static IReadOnlyDictionary<string, object> CreateListenerMetadata(EventSubscriptionId sourceSubscriptionId)
    {
        return new Dictionary<string, object>
        {
            [IsTestListener] = true,
            [SourceSubscriptionId] = sourceSubscriptionId.ToString()
        };
    }

    /// <summary>
    /// Determines whether a subscription is owned by the EventBus UI test-listener feature.
    /// </summary>
    /// <param name="subscription">Subscription to inspect.</param>
    /// <returns><see langword="true"/> when the subscription is a temporary test listener.</returns>
    public static bool IsTestListenerSubscription(IEventSubscription subscription)
    {
        return subscription.GetMetadata<bool>(IsTestListener);
    }
}
