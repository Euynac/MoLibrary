# Entity, Request, and Event Template

Use `$ContractNamespace$` for the contract project selected by the architecture skill, such as `Platform.Protocol.PublishedLanguages.DomainOrdering`. Use `$DomainNamespace$` for the domain project namespace selected by the architecture skill, such as `OrderingService.Domain` or `Domains.Ordering`.

## Request Contract

Use request contracts for use-case input. Keep them stable and serializable.

```csharp
using Monica.WebApi.Abstractions;
using Monica.WebApi.Annotations;

namespace $ContractNamespace$.Requests;

[ApiEndpoint(ApiHttpMethod.Get, "$QueryRoute$", Binding = ApiRequestBinding.Query)]
public sealed record Query$FeatureName$(long Id) : IResultRequest<$ResponseName$>;

[ApiEndpoint(ApiHttpMethod.Post, "$CommandRoute$", Binding = ApiRequestBinding.Body)]
public sealed record Command$FeatureName$(long Id, string Reason) : IResultRequest;
```

Rules:

- Use request types for commands and queries, not for persistence state.
- Keep request contracts small and explicit.
- Place response DTOs near contracts, not near persistence.
- Let `[ApiEndpoint]` own the HTTP verb, relative route, request binding, and optional operation name. Generated handlers must not repeat these with MVC attributes.
- Requests in the exact `*.PublishedLanguages.Domain{DomainName}.Requests` namespace are published RPC contracts when their source assembly has matching `WebApiGenerationConfig`; attributed requests elsewhere are local HTTP contracts only.
- Published RPC result envelopes must implement `IRemoteResultEnvelope<TSelf>`. Built-in `Res`, `Res<T>`, and `ResPaged<T>` already do; custom envelopes must construct remote failures through `CreateRemoteFailure` without bypassing their invariants.
- Treat moving a request into or out of `PublishedLanguages` as a public compatibility decision because it changes RPC client generation.

## Entity

Use entities to own identity, state transitions, and invariants.

```csharp
using Monica.Repository.Entity.Abstractions;

namespace $DomainNamespace$.Entities;

public sealed class Order : Entity<long>
{
    public string Number { get; private set; } = string.Empty;
    public bool IsApproved { get; private set; }

    public Order(long id, string number)
    {
        Id = id;
        Rename(number);
    }

    public void Rename(string number)
    {
        if (string.IsNullOrWhiteSpace(number))
        {
            throw new ArgumentException("Order number is required.", nameof(number));
        }

        Number = number.Trim();
    }

    public void Approve()
    {
        if (IsApproved)
        {
            throw new InvalidOperationException("Order is already approved.");
        }

        IsApproved = true;
    }

    public override void AutoSetNewId(bool notSetWhenNotDefault = false)
    {
        if (notSetWhenNotDefault && Id != default)
        {
            return;
        }

        Id = 0;
    }
}
```

Rules:

- Keep behavior close to the state it protects.
- Avoid public setters unless a property is intentionally unconstrained.
- Keep EF mapping concerns in persistence configuration or `DbContext`, not in the request contract.

## Domain Event

Use domain events to describe meaningful business facts.

```csharp
using Monica.EventBus.Events;

namespace $ContractNamespace$.Events;

public sealed class EventOrderApproved : DomainEvent
{
    public long OrderId { get; init; }
    public string Number { get; init; } = string.Empty;
}
```

Rules:

- Name events after facts, not handlers.
- Keep event payloads stable, serializable, and focused on what consumers need.
- Do not expose the full entity graph in an event.
