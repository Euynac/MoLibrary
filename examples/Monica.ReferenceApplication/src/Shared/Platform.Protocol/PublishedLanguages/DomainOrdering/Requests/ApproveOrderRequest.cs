using Monica.WebApi.Abstractions;
using Platform.Protocol.PublishedLanguages.DomainOrdering.Models;

namespace Platform.Protocol.PublishedLanguages.DomainOrdering.Requests;

/// <summary>
/// Requests the one-way transition of a draft order to approved.
/// </summary>
/// <param name="OrderId">The order identifier.</param>
public sealed record ApproveOrderRequest(Guid OrderId) : IResultRequest<OrderDto>;
