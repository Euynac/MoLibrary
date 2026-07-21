using Monica.WebApi.Abstractions;
using Monica.WebApi.Annotations;
using Platform.Protocol.PublishedLanguages.DomainOrdering.Models;

namespace Platform.Protocol.PublishedLanguages.DomainOrdering.Requests;

/// <summary>
/// Requests creation of a draft order.
/// </summary>
/// <param name="OrderNumber">The unique business-visible order number.</param>
/// <param name="CustomerName">The customer display name.</param>
/// <param name="Total">The positive order total.</param>
[ApiEndpoint(
    ApiHttpMethod.Post,
    "orders",
    Binding = ApiRequestBinding.Body)]
public sealed record CommandCreateOrder(
    string OrderNumber,
    string CustomerName,
    decimal Total) : IResultRequest<OrderDto>;
