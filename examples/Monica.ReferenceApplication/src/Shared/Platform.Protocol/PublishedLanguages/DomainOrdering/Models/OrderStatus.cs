namespace Platform.Protocol.PublishedLanguages.DomainOrdering.Models;

/// <summary>
/// Describes the lifecycle state of an order.
/// </summary>
public enum OrderStatus
{
    /// <summary>
    /// The order can still be reviewed before approval.
    /// </summary>
    Draft,

    /// <summary>
    /// The order has passed its final approval transition.
    /// </summary>
    Approved
}
