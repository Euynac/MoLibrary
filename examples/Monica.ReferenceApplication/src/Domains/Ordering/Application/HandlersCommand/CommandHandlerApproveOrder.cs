using Domains.Ordering.Interfaces;
using Domains.Ordering.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Monica.Core.Results;
using Monica.EventBus.Abstractions;
using Monica.Repository.UnitOfWork.Abstractions;
using Monica.WebApi.Abstractions;
using Platform.Protocol.PublishedLanguages.DomainOrdering.Events;
using Platform.Protocol.PublishedLanguages.DomainOrdering.Models;
using Platform.Protocol.PublishedLanguages.DomainOrdering.Requests;

namespace Domains.Ordering.Application.HandlersCommand;

/// <summary>
/// Approves an existing order through its one-way domain transition.
/// </summary>
public sealed class CommandHandlerApproveOrder(
    IRepositoryOrder repository,
    IUnitOfWorkManager unitOfWorkManager,
    ILocalEventBus localEventBus,
    ILoggerFactory loggerFactory)
    : ApplicationService<ApproveOrderRequest, OrderDto>(loggerFactory)
{
    /// <summary>
    /// Approves the requested draft order.
    /// </summary>
    /// <param name="request">The approval request.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The approved order, or a boundary failure when it is missing or already approved.</returns>
    [HttpPost("orders/approve")]
    public override async Task<Res<OrderDto>> Handle(
        ApproveOrderRequest request,
        CancellationToken cancellationToken)
    {
        var order = await repository.FindAsync(request.OrderId, cancellationToken);
        if (order is null)
        {
            return Res.Fail($"Order '{request.OrderId}' was not found.");
        }

        try
        {
            var approvedAtUtc = DateTimeOffset.UtcNow;
            order.Approve(approvedAtUtc);
            await repository.SaveAsync(order, cancellationToken);

            var approvalEvent = new EventOrderApproved
            {
                OrderId = order.Id,
                OrderNumber = order.OrderNumber,
                ApprovedAtUtc = approvedAtUtc
            };

            if (unitOfWorkManager.Current is { } unitOfWork)
            {
                unitOfWork.OnCompleted(() => localEventBus.PublishAsync(approvalEvent));
            }
            else
            {
                await localEventBus.PublishAsync(
                    approvalEvent,
                    cancellationToken: cancellationToken);
            }

            return Res.Ok(UtilsOrderMapping.ToDto(order));
        }
        catch (InvalidOperationException exception)
        {
            return Res.Fail(exception.Message);
        }
    }
}
