# Event Handler Template

## Use When

- Another unit should react to an event without direct coupling to the sender.
- The reaction is still synchronous enough to stay in an event handler rather than being moved into a job queue.

## Rules

- Use `DomainEventHandler<TEvent>` for distributed events.
- Use `LocalEventHandler<TEvent>` for in-process reactions.
- Use the inherited `Logger` in handler methods when logging is needed. Monica resolves it after activation from the
  host that owns the handler instance; do not access it from a constructor.
- Keep handlers thin. Delegate reusable logic to `DomainService`.
- Make the event type stable before adding consumers.
- Use `$ApplicationNamespace$` for the application-layer namespace chosen by the architecture skill.
- Place handlers in `HandlersEvent/`.

## Distributed Handler Example

```csharp
using Monica.ProjectUnits.Annotations;
using Monica.WebApi.Abstractions;

namespace $ApplicationNamespace$.HandlersEvent;

[ProjectUnitMetadata(
    "Notify Warehouse After Order Approval",
    Owner = "$Owner$",
    Description = "Coordinates the warehouse reaction to an approved order.",
    Tags = ["$SubdomainTag$", "$FeatureTag$"])]
[ProjectUnitRequirement("$RequirementId$")]
public sealed class DomainEventHandlerOrderApproved(DomainNotifyWarehouse domainService)
    : DomainEventHandler<EventOrderApproved>
{
    public override async Task HandleEventAsync(
        EventOrderApproved eventData,
        CancellationToken cancellationToken)
    {
        await domainService.ExecuteAsync(eventData.OrderId, cancellationToken);
    }
}
```

## Local Handler Example

```csharp
using Monica.ProjectUnits.Annotations;
using Monica.WebApi.Abstractions;

namespace $ApplicationNamespace$.HandlersEvent;

[ProjectUnitMetadata(
    "Refresh Order Read Model",
    Owner = "$Owner$",
    Description = "Refreshes the local read model after order approval.",
    Tags = ["$SubdomainTag$", "$FeatureTag$"])]
[ProjectUnitRequirement("$RequirementId$")]
public sealed class LocalEventHandlerOrderApproved(DomainRefreshReadModel domainService)
    : LocalEventHandler<EventOrderApproved>
{
    public override async Task HandleEventAsync(
        EventOrderApproved eventData,
        CancellationToken cancellationToken)
    {
        await domainService.ExecuteAsync(eventData.OrderId, cancellationToken);
    }
}
```

## Notes

- If the reaction is slow, retriable, or should survive process restarts, move the heavy work into a `TriggeredJob`.
- Pass the handler `CancellationToken` into every cancellable dependency. A distributed delivery timeout can return
  the message for retry, but it cannot forcibly terminate handler code that ignores cancellation.
- Do not let handlers become alternate application services with large control flow and validation logic.
