# ApplicationService Template

## Variables

- `$FeatureName$`: use-case name, such as `GetOrder` or `ApproveOrder`
- `$RequestName$`: request type, such as `QueryGetOrder` or `CommandApproveOrder`
- `$RequestRoute$`: request-specific route segment, such as `tree`, `publish`, or `approve`
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
- Keep the controller-level route on the default generator convention when possible. Prefer `api/{version}/{DomainName(PascalCase)}` as the base route and put only the request-specific segment on the handler method.
- In a modular monolith, place the assembly-level `AutoControllerConfig(DefaultRoutePrefix = "api/v1", DomainName = "{Subdomain}")` in the domain project root.
- In a microservice, place the same `AutoControllerConfig` in `{Subdomain}Service.API/Program.cs`.
- Place handlers in `HandlersCommand/` or `HandlersQuery/` under the application layer chosen by the architecture skill.

## Routing Convention

- Prefer the composed route shape `api/{version}/{DomainName(PascalCase)}/{request-route}`.
- Let the architecture-level default route config provide `api/{version}/{DomainName(PascalCase)}` once per domain or service.
- Put the request-specific segment on the handler method, such as `QueryHandlerGetDocTree` with `[HttpGet("tree")]`, which produces `GET api/v1/Documentation/tree`.

## Minimal Query Example

```csharp
using Microsoft.AspNetCore.Mvc;
using Monica.Core.Results;
using Monica.Repository.Persistence.Abstractions;
using Monica.WebApi.Abstractions;

namespace $ApplicationNamespace$.HandlersQuery;

public sealed record Query$FeatureName$(long Id) : IResultRequest<$ResponseName$>;

public sealed class QueryHandler$FeatureName$(
    $RepositoryName$ repository)
    : ApplicationService<Query$FeatureName$, $ResponseName$>
{
    [HttpGet("$RequestRoute$")]
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
using Microsoft.AspNetCore.Mvc;
using Monica.Core.Results;
using Monica.WebApi.Abstractions;

namespace $ApplicationNamespace$.HandlersCommand;

public sealed record Command$FeatureName$(long Id) : IResultRequest;

public sealed class CommandHandler$FeatureName$(
    Domain$FeatureName$ domainService)
    : ApplicationService<Command$FeatureName$>
{
    [HttpPost("$RequestRoute$")]
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
