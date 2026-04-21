# ProjectUnit Naming, Placement, and Boundaries

Use these rules to keep Monica project-unit discovery, folder layout, code navigation, and DDD boundaries aligned.

## Default Naming Rules

These conventions mirror the current `Monica.Framework/ProjectUnits` default discovery rules. If you drift away from them, the framework can still compile, but project-unit discovery and solution scanning become less reliable.

| Unit | ProjectUnits default convention | Recommended naming |
| --- | --- | --- |
| `ApplicationService` | Class name contains `Handler` | Prefer `CommandHandler*` or `QueryHandler*` |
| `DomainService` | Class name starts with `Domain` | `Domain*` |
| `Repository` implementation | Class name starts with `Repository` | Implementation `Repository*`, interface `IRepository*` |
| `DomainEvent` | Class name starts with `Event` | `Event*` |
| `DomainEventHandler` | Class name starts with `DomainEventHandler` | `DomainEventHandler*` |
| `LocalEventHandler` | Class name starts with `LocalEventHandler` | `LocalEventHandler*` |
| `Entity` | Namespace contains `Entities` | Place under `Entities/` |
| `Configuration` | Class name ends with `Options` | `*Options` |
| `RecurringJob` | Class name starts with `Worker` | `Worker*` |
| `TriggeredJob` | Class name starts with `Job` | `Job*` |
| `RequestDto` | Implements `IResultRequest<T>` or `IResultRequest` | Prefer `Command*`, `Query*`, or `Request*` |

## Additional Support-Type Conventions

These are not part of `Monica.Framework/ProjectUnits` discovery, but they are part of the recommended business-project layout.

| Support type | Recommended naming | Purpose |
| --- | --- | --- |
| `Utility` helper | `Utils*` | Pure helper code that stays inside the domain boundary and does not deserve its own `DomainService` |

## Placement Rules

Use the same leaf folder semantics across solution styles. Keep folders flat and rely on prefix naming before introducing extra sub-folders.

| Unit | Microservice placement | Modular monolith placement |
| --- | --- | --- |
| `ApplicationService` command | `{Subdomain}Service.API/HandlersCommand/` | `Domains/{Subdomain}/Application/HandlersCommand/` |
| `ApplicationService` query | `{Subdomain}Service.API/HandlersQuery/` | `Domains/{Subdomain}/Application/HandlersQuery/` |
| `DomainEventHandler` / `LocalEventHandler` | `{Subdomain}Service.API/HandlersEvent/` | `Domains/{Subdomain}/Application/HandlersEvent/` |
| `RecurringJob` / `TriggeredJob` | `{Subdomain}Service.API/BackgroundWorkers/` | `Domains/{Subdomain}/Application/BackgroundWorkers/` |
| `DomainService` | `{Subdomain}Service.Domain/DomainServices/` | `Domains/{Subdomain}/DomainServices/` |
| `RequestDto` | `Shared/Platform.Protocol/PublishedLanguages/Domain{Subdomain}/Requests/` | `Shared/Platform.Protocol/PublishedLanguages/Domain{Subdomain}/Requests/` |
| `DomainEvent` | `Shared/Platform.Protocol/PublishedLanguages/Domain{Subdomain}/Events/` | `Shared/Platform.Protocol/PublishedLanguages/Domain{Subdomain}/Events/` |
| `Entity` | `{Subdomain}Service.Domain/Entities/` | `Domains/{Subdomain}/Entities/` |
| `Repository` interface | `{Subdomain}Service.Domain/Interfaces/` | `Domains/{Subdomain}/Interfaces/` |
| `Repository` implementation | `{Subdomain}Service.Domain/Repository/` | `Domains/{Subdomain}/Repository/` |
| `DbContext` / EF mapping | `{Subdomain}Service.Domain/Repository/` | `Domains/{Subdomain}/Repository/` |
| `Configuration` | `{Subdomain}Service.Domain/Configurations/` | `Domains/{Subdomain}/Configurations/` |
| `Utility` helper | `{Subdomain}Service.Domain/Utilities/` | `Domains/{Subdomain}/Utilities/` |

## Folder Scanability Rules

- Prefer prefix naming inside a folder instead of adding sub-folders with one or two files.
- Keep leaf folders directly recognizable in the IDE: `QueryHandlerGetOrder.cs`, `CommandHandlerApproveOrder.cs`, `DomainEventHandlerOrderApproved.cs`, `WorkerRefreshOrderSnapshot.cs`.
- Use `HandlersEvent` as the standard event-handler folder name. Do not alternate between `HandlersEvent` and `EventHandlers`.
- Keep AppHost or gateway entry projects as composition only. Do not place business ProjectUnits there.
- Put pure helper code in `Utilities/` with `Utils*` names. If a type starts accumulating orchestration or domain rules, move it into a `DomainService` or entity instead.

## Boundary Rules

- Keep `Res` and `Res<T>` at the `ApplicationService` boundary. Domain services and repositories should prefer normal return types and exceptions.
- Keep orchestration in `ApplicationService`, not business rules. If a handler becomes hard to explain in one paragraph, move the reusable rule into a `DomainService` or the entity itself.
- Keep entity invariants on the entity. Do not let handlers or repositories directly toggle domain state through scattered property assignments.
- Keep repository interfaces on the domain side of the boundary and repository implementations on the infrastructure side.
- For modular monolith projects, the merged `Domains.{Subdomain}.csproj` is the infrastructure side for that subdomain, so repository implementations stay under `Domains/{Subdomain}/Repository/`.
- For microservice projects, repository implementations stay in `{Subdomain}Service.Domain/Repository/`.
- Keep libraries that are only used by one repository or adapter in the owning subdomain/service project. Do not move that implementation into `Platform` unless the implementation itself is project-common reusable infrastructure.
- Keep request, response, and event contracts separate from persistence entities.
- Keep adapter code thin. HTTP attributes, endpoint registration, gRPC definitions, or background-host wiring should not replace ProjectUnits.

## Practical Heuristics

- Add a new `DomainService` when two or more entry points need the same business rule.
- Add a value object when a concept has rules but no identity.
- Add a new repository method only for meaningful domain queries. Do not wrap every basic CRUD call in a bespoke method.
- Add a new event only when another unit can react without depending on the caller's internal flow.

## Result Handling

- For typed success results, prefer `return data;` or `return Res.Ok<TResponse>(data);`.
- For failure results, prefer `return Res.Fail("message");`.
- If `TResponse` is `string`, use `Res.Ok<string>(value)` instead of `Res.Ok(value)`.
