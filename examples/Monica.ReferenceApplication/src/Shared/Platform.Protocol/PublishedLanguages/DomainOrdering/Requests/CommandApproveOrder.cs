using Monica.WebApi.Abstractions;
using Monica.WebApi.Annotations;
using Platform.Protocol.PublishedLanguages.DomainOrdering.Models;

namespace Platform.Protocol.PublishedLanguages.DomainOrdering.Requests;

/// <summary>
/// Requests the one-way transition of a draft order to approved.
/// </summary>
/// <param name="OrderId">The order identifier.</param>
[ApiEndpoint(
    ApiHttpMethod.Post,
    "orders/approve",
    Binding = ApiRequestBinding.Body)]
public sealed record CommandApproveOrder(Guid OrderId) : IResultRequest<OrderDto>;
