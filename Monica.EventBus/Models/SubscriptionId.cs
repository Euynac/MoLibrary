namespace Monica.EventBus.Models;

/// <summary>
/// Strongly-typed subscription identifier.
/// </summary>
public readonly record struct SubscriptionId(Guid Value)
{
    /// <summary>
    /// Creates a new unique subscription ID.
    /// </summary>
    public static SubscriptionId NewId() => new(Guid.NewGuid());

    /// <summary>
    /// Returns a string representation of the subscription ID in "N" format (32 hex digits).
    /// </summary>
    public override string ToString() => Value.ToString("N");

    public static implicit operator Guid(SubscriptionId id) => id.Value;
    public static implicit operator SubscriptionId(Guid guid) => new(guid);
}
