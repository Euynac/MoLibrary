# Event Handler Template

## Use When

- Another unit should react to an event without direct coupling to the sender.
- The reaction is still synchronous enough to stay in an event handler rather than being moved into a job queue.

## Rules

- Use `DomainEventHandler<TEvent>` for distributed events.
- Use `LocalEventHandler<TEvent>` for in-process reactions.
- Keep handlers thin. Delegate reusable logic to `DomainService`.
- Make the event type stable before adding consumers.
- Use `$EntryProjectNamespace$` for the boundary project namespace chosen by the architecture skill.

## Distributed Handler Example

```csharp
using Monica.WebApi.Abstractions;

namespace $EntryProjectNamespace$.HandlersEvent;

public sealed class DomainEventHandlerOrderApproved(
    DomainNotifyWarehouse domainService)
    : DomainEventHandler<EventOrderApproved>
{
    public override async Task HandleEventAsync(EventOrderApproved eventData)
    {
        await domainService.ExecuteAsync(eventData.OrderId);
    }
}
```

## Local Handler Example

```csharp
using Monica.WebApi.Abstractions;

namespace $EntryProjectNamespace$.HandlersEvent;

public sealed class LocalEventHandlerOrderApproved(
    DomainRefreshReadModel domainService)
    : LocalEventHandler<EventOrderApproved>
{
    public override async Task HandleEventAsync(EventOrderApproved eventData)
    {
        await domainService.ExecuteAsync(eventData.OrderId);
    }
}
```

## Notes

- If the reaction is slow, retriable, or should survive process restarts, move the heavy work into a `TriggeredJob`.
- Do not let handlers become alternate application services with large control flow and validation logic.
