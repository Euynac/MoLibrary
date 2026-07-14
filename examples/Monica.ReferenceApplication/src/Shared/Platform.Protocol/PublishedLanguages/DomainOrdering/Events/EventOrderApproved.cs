using Monica.EventBus.Events;

namespace Platform.Protocol.PublishedLanguages.DomainOrdering.Events;

/// <summary>
/// Describes the committed approval of an order.
/// </summary>
public sealed class EventOrderApproved : DomainEvent
{
    /// <summary>
    /// Gets the approved order identifier.
    /// </summary>
    public required Guid OrderId { get; init; }

    /// <summary>
    /// Gets the business-visible order number.
    /// </summary>
    public required string OrderNumber { get; init; }

    /// <summary>
    /// Gets when the approval occurred.
    /// </summary>
    public required DateTimeOffset ApprovedAtUtc { get; init; }
}
