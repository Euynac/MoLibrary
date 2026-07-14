using Domains.Ordering.Entities;
using Domains.Ordering.Interfaces;
using Domains.Ordering.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Monica.Core.Results;
using Monica.WebApi.Abstractions;
using Platform.Protocol.PublishedLanguages.DomainOrdering.Models;
using Platform.Protocol.PublishedLanguages.DomainOrdering.Requests;

namespace Domains.Ordering.Application.HandlersCommand;

/// <summary>
/// Creates a validated draft order and returns its public representation.
/// </summary>
public sealed class CommandHandlerCreateOrder(
    IRepositoryOrder repository,
    ILoggerFactory loggerFactory)
    : ApplicationService<CreateOrderRequest, OrderDto>(loggerFactory)
{
    /// <summary>
    /// Creates an order after applying entity and repository invariants.
    /// </summary>
    /// <param name="request">The order creation request.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The created order, or a boundary failure when input or uniqueness rules are violated.</returns>
    [HttpPost("orders")]
    public override async Task<Res<OrderDto>> Handle(
        CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var order = Order.Create(request.OrderNumber, request.CustomerName, request.Total);
            await repository.AddAsync(order, cancellationToken);
            return Res.Ok(UtilsOrderMapping.ToDto(order));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return Res.Fail(exception.Message);
        }
    }
}
