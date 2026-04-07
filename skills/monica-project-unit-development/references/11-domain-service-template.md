# DomainService Template

Use `$DomainNamespace$` for the domain project namespace selected by the architecture skill, such as `OrderingService.Domain` or `Ordering.Domain`.

## Use When

- The business rule is reused by multiple entry points.
- The logic spans more than one entity or repository.
- You need a stable unit for orchestration that is still inside the domain boundary.

## Rules

- Derive from `DomainService`.
- Start the class name with `Domain`.
- Prefer normal return types and exceptions. Let `ApplicationService` translate failures into `Res`.
- Keep persistence access delegated to repositories.
- Keep state mutation on entities whenever the entity is the natural owner of the rule.

## Minimal Example

```csharp
using Monica.WebApi.Abstractions;

namespace $DomainNamespace$.DomainServices;

public sealed class Domain$FeatureName$(
    IRepositoryOrder repository)
    : DomainService
{
    public async Task<Order> ExecuteAsync(
        long orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await repository.GetAsync(orderId, cancellationToken: cancellationToken);

        order.Approve();

        await repository.UpdateAsync(order, autoSave: true, cancellationToken: cancellationToken);
        return order;
    }
}
```

## Heuristics

- If the method only wraps a simple query, the repository may be the better home.
- If the method mostly toggles fields on one entity, move that behavior onto the entity.
- If the method starts coordinating transport or UI concerns, it is no longer a `DomainService`.
