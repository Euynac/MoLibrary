using Microsoft.Extensions.Logging;
using Monica.WebApi.Abstractions;
using Platform.Protocol.PublishedLanguages.DomainOrdering.Events;

namespace Domains.Ordering.Application.HandlersEvent;

/// <summary>
/// Records the in-process reaction to a committed order approval.
/// </summary>
public sealed class LocalEventHandlerOrderApproved(ILoggerFactory loggerFactory)
    : LocalEventHandler<EventOrderApproved>(loggerFactory)
{
    /// <inheritdoc />
    public override Task HandleEventAsync(EventOrderApproved eventData)
    {
        Logger.LogInformation(
            "Observed committed approval for order {OrderNumber} ({OrderId}) at {ApprovedAtUtc}.",
            eventData.OrderNumber,
            eventData.OrderId,
            eventData.ApprovedAtUtc);

        return Task.CompletedTask;
    }
}
