namespace Platform.Protocol.PublishedLanguages.DomainOrdering.Models;

/// <summary>
/// Represents the stable public view of an order.
/// </summary>
/// <param name="Id">The order identifier.</param>
/// <param name="OrderNumber">The business-visible order number.</param>
/// <param name="CustomerName">The customer display name.</param>
/// <param name="Total">The order total.</param>
/// <param name="Status">The current lifecycle state.</param>
/// <param name="CreatedAtUtc">When the order was created.</param>
/// <param name="ApprovedAtUtc">When the order was approved, or <see langword="null"/> while it is a draft.</param>
public sealed record OrderDto(
    Guid Id,
    string OrderNumber,
    string CustomerName,
    decimal Total,
    OrderStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ApprovedAtUtc);
