# ApplicationService Template

## Variables

- `$FeatureName$`: use-case name, such as `GetOrder` or `ApproveOrder`
- `$RequestName$`: request type, such as `QueryGetOrder` or `CommandApproveOrder`
- `$RequestRoute$`: request-specific route segment, such as `tree`, `publish`, or `approve`
- `$OperationName$`: stable generated method name, such as `GetOrder` or `ApproveOrder`
- `$ResponseName$`: response DTO type when the handler returns data
- `$RepositoryName$`: repository abstraction, such as `IRepositoryOrder`
- `$ApplicationNamespace$`: the application-layer namespace chosen by the architecture skill, such as `OrderingService.API` or `Domains.Ordering.Application`

## Use When

- You need a new externally visible use case.
- The entry point should return `Res` or `Res<T>`.
- The use case orchestrates domain logic, repositories, and mapping, but does not own the business rules itself.

## Rules

- Derive from `ApplicationService<TRequest, TResponse>` or `ApplicationService<TRequest>`.
- Use the inherited `Logger` in handler methods when logging is needed. Monica resolves it after activation from the
  host that owns the service instance; do not access it from a constructor.
- Make the request implement `IResultRequest<TResponse>` or `IResultRequest`.
- Keep the handler thin. Push reusable rules into `DomainService` or the entity itself.
- Catch exceptions only when you are adding boundary-specific context. Do not smother useful failures.
- Put `[ApiEndpoint]` on the request type. It owns the HTTP method, request-specific route, binding source, and optional generated operation name.
- Do not put MVC `[Http*]`, `[Route]`, or `[From*]` attributes on the handler. The handler owns execution only.
- For a published RPC contract, place the attributed request in `Platform.Protocol.PublishedLanguages.Domain{Subdomain}.Requests` and configure its protocol assembly with `WebApiGenerationConfig` plus the required `RpcClientTargets`.
- For a local-only HTTP endpoint, keep the attributed request outside `PublishedLanguages` and configure its owning assembly with `WebApiGenerationConfig`. Use `RpcClientTargets.None` unless that assembly also owns published contracts.
- If the mediator request must remain entirely internal, do not derive its handler from `ApplicationService`; implement `IRequestHandler<TRequest, TResult>` and a transient dependency marker directly. Missing `[ApiEndpoint]` on an `ApplicationService` is a generator error, not an opt-out switch.
- Place handlers in `HandlersCommand/` or `HandlersQuery/` under the application layer chosen by the architecture skill.

## Routing Convention

- Prefer the composed route shape `api/{version}/{DomainName(PascalCase)}/{request-route}`.
- Let `WebApiGenerationConfig` provide the route prefix and domain once per contract domain.
- Put only the request-specific segment on `[ApiEndpoint]`, such as `[ApiEndpoint(ApiHttpMethod.Get, "tree")]`, which produces `GET api/v1/Documentation/tree`.
- Use `ApiRequestBinding.Query` for query-string requests and `ApiRequestBinding.Body` for JSON commands when `Auto` would make the public contract less obvious.

For a protocol assembly that publishes HTTP and local RPC clients:

```csharp
using Monica.WebApi.Annotations;

[assembly: WebApiGenerationConfig(
    "api/v1",
    DomainName = "$DomainName$",
    RpcClientTargets = RpcClientGenerationTargets.Http | RpcClientGenerationTargets.Local)]
```

For an assembly that owns only local HTTP endpoints, use the same configuration with `RpcClientTargets.None`. The generator still creates controllers for its attributed requests but does not publish requests outside the strict published-language namespace.

## Minimal Query Example

```csharp
using Monica.Core.Results;
using Monica.ProjectUnits.Annotations;
using Monica.Repository.Persistence.Abstractions;
using Monica.WebApi.Abstractions;
using Monica.WebApi.Annotations;

namespace $ApplicationNamespace$.HandlersQuery;

[ApiEndpoint(
    ApiHttpMethod.Get,
    "$RequestRoute$",
    Binding = ApiRequestBinding.Query,
    OperationName = "$OperationName$")]
[ProjectUnitMetadata(
    "$FeatureName$ Query",
    Owner = "$Owner$",
    Description = "Requests the $FeatureName$ use case.",
    Tags = ["$SubdomainTag$", "$FeatureTag$"])]
[ProjectUnitRequirement("$RequirementId$")]
public sealed record Query$FeatureName$(long Id) : IResultRequest<$ResponseName$>;

[ProjectUnitMetadata(
    "$FeatureName$",
    Owner = "$Owner$",
    Description = "Coordinates the $FeatureName$ query boundary.",
    Tags = ["$SubdomainTag$", "$FeatureTag$"])]
[ProjectUnitRequirement("$RequirementId$")]
public sealed class QueryHandler$FeatureName$($RepositoryName$ repository)
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
using Monica.ProjectUnits.Annotations;
using Monica.WebApi.Abstractions;
using Monica.WebApi.Annotations;

namespace $ApplicationNamespace$.HandlersCommand;

[ApiEndpoint(
    ApiHttpMethod.Post,
    "$RequestRoute$",
    Binding = ApiRequestBinding.Body,
    OperationName = "$OperationName$")]
[ProjectUnitMetadata(
    "$FeatureName$ Command",
    Owner = "$Owner$",
    Description = "Requests the $FeatureName$ use case.",
    Tags = ["$SubdomainTag$", "$FeatureTag$"])]
[ProjectUnitRequirement("$RequirementId$")]
public sealed record Command$FeatureName$(long Id) : IResultRequest;

[ProjectUnitMetadata(
    "$FeatureName$",
    Owner = "$Owner$",
    Description = "Coordinates the $FeatureName$ command boundary.",
    Tags = ["$SubdomainTag$", "$FeatureTag$"])]
[ProjectUnitRequirement("$RequirementId$")]
public sealed class CommandHandler$FeatureName$(Domain$FeatureName$ domainService)
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
- RPC publication is namespace-driven, not handler-driven. Do not move a local request into `PublishedLanguages` merely to share a DTO.
