# ApplicationService Template

## Variables

- `$FeatureName$`: use-case name, such as `GetOrder` or `ApproveOrder`
- `$RequestName$`: request type, such as `QueryGetOrder` or `CommandApproveOrder`
- `$ResponseName$`: response DTO type when the handler returns data
- `$RepositoryName$`: repository abstraction, such as `IRepositoryOrder`
- `$ApplicationNamespace$`: the application-layer namespace chosen by the architecture skill, such as `OrderingService.API` or `Domains.Ordering.Application`

## Use When

- You need a new externally visible use case.
- The entry point should return `Res` or `Res<T>`.
- The use case orchestrates domain logic, repositories, and mapping, but does not own the business rules itself.

## Rules

- Derive from `ApplicationService<TRequest, TResponse>` or `ApplicationService<TRequest>`.
- Make the request implement `IResultRequest<TResponse>` or `IResultRequest`.
- Keep the handler thin. Push reusable rules into `DomainService` or the entity itself.
- Catch exceptions only when you are adding boundary-specific context. Do not smother useful failures.
- Add transport-specific attributes only if your chosen host conventions require them.
- Place handlers in `HandlersCommand/` or `HandlersQuery/` under the application layer chosen by the architecture skill.

## Minimal Query Example

```csharp
using Monica.Core.Results;
using Monica.Repository.Persistence.Abstractions;
using Monica.WebApi.Abstractions;

namespace $ApplicationNamespace$.HandlersQuery;

public sealed record Query$FeatureName$(long Id) : IResultRequest<$ResponseName$>;

public sealed class QueryHandler$FeatureName$(
    $RepositoryName$ repository)
    : ApplicationService<Query$FeatureName$, $ResponseName$>
{
    public override async Task<Res<$ResponseName$>> Handle(
        Query$FeatureName$ request,
        CancellationToken cancellationToken)
    {
        var entity = await repository.FindAsync(request.Id, cancellationToken: cancellationToken);
        if (entity is null)
        {
            return Res.Fail("$FeatureName$ target was not found.");
        }

        return Res.Ok(new $ResponseName$
        {
            Id = entity.Id
        });
    }
}
```

## Minimal Command Example

```csharp
using Monica.Core.Results;
using Monica.WebApi.Abstractions;

namespace $ApplicationNamespace$.HandlersCommand;

public sealed record Command$FeatureName$(long Id) : IResultRequest;

public sealed class CommandHandler$FeatureName$(
    Domain$FeatureName$ domainService)
    : ApplicationService<Command$FeatureName$>
{
    public override async Task<Res> Handle(
        Command$FeatureName$ request,
        CancellationToken cancellationToken)
    {
        await domainService.ExecuteAsync(request.Id, cancellationToken);
        return Res.Ok();
    }
}
```

## Notes

- If the success payload type is `string`, use `Res.Ok<string>(value)`.
- If a query becomes complex because of filtering, paging, or policy checks, extract that logic before the handler turns procedural.
