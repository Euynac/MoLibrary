using Monica.WebApi.Abstractions;
using Platform.Protocol.PublishedLanguages.DomainOrdering.Models;

namespace Platform.Protocol.PublishedLanguages.DomainOrdering.Requests;

/// <summary>
/// Requests all orders in reverse creation order.
/// </summary>
public sealed record GetOrdersRequest : IResultRequest<IReadOnlyList<OrderDto>>;
