namespace Monica.EventBus.Constants;

/// <summary>
/// Standard metadata keys used for subscriptions.
/// </summary>
public static class SubscriptionMetadataKeys
{
    /// <summary>
    /// Method name of an action-based handler.
    /// </summary>
    public const string ActionMethodName = "ActionMethodName";

    /// <summary>
    /// Fully qualified declaring type name of an action-based handler.
    /// </summary>
    public const string ActionDeclaringType = "ActionDeclaringType";

    /// <summary>
    /// Human-readable method signature of an action-based handler.
    /// </summary>
    public const string ActionMethodSignature = "ActionMethodSignature";

    /// <summary>
    /// Indicates whether the action-based handler method is static.
    /// </summary>
    public const string ActionIsStatic = "ActionIsStatic";
}
