# ProjectUnit Overview

Use this file to decide which Monica-native ProjectUnit should own a new piece of business behavior.

The current Monica project-unit discovery logic recognizes these core patterns:

| Unit | Base type or contract | Naming convention | Primary responsibility |
| --- | --- | --- | --- |
| `ApplicationService` | `ApplicationService<TRequest, TResponse>` or `ApplicationService<TRequest>` | `CommandHandler*`, `QueryHandler*`, or another `*Handler*` name | Boundary use case orchestration that returns `Res` |
| `RequestDto` | `IResultRequest<TResponse>` or `IResultRequest` | `Command*`, `Query*`, `Request*` | Stable request contract for a use case |
| `DomainService` | `DomainService` | `Domain*` | Reusable domain logic that spans multiple entities or workflows |
| `Entity` | `Entity<TKey>` or `Entity` plus `IEntity` | Place under `Entities/` | Own state, invariants, and behavior |
| `Repository` | `IRepository<TEntity>` or `IRepository<TEntity, TKey>` plus implementation | `IRepository*` and `Repository*` | Persistence access for one aggregate or entity family |
| `DomainEvent` | `DomainEvent` or `IDomainEvent` | `Event*` | A business fact worth publishing or reacting to |
| `DomainEventHandler` | `DomainEventHandler<TEvent>` or `IDistributedEventHandler<TEvent>` | `DomainEventHandler*` | React to distributed events |
| `LocalEventHandler` | `LocalEventHandler<TEvent>` or `ILocalEventHandler<TEvent>` | `LocalEventHandler*` | React to in-process events |
| `Configuration` | `[Configuration]` class | `*Options` | Typed runtime configuration |
| `RecurringJob` | `RecurringJob` | `Worker*` | Scheduled background work |
| `TriggeredJob` | `TriggeredJob<TArgs>` | `Job*` | On-demand asynchronous work |

## Selection Rules

- Create a new `ApplicationService` when you need a new externally visible use case.
- Create a `DomainService` when the business rule must be reused by multiple handlers, jobs, or event handlers.
- Create or extend an `Entity` when the rule belongs to the state owner itself.
- Create a `Repository` only when persistence access is needed. Do not hide pure business rules inside repositories.
- Create a `DomainEvent` only when the fact itself matters to other handlers or modules. Do not emit events for trivial internal control flow.
- Create a `Configuration` class only for genuine host-configurable behavior, not for constants.

## Placement

This skill does not decide the outer folder layout.

- Use `monica-business-microservice` for `Shared/PublishedLanguages`, `Services/{Subdomain}`, and migration project placement.
- Use `monica-business-modular-monolith` for `Modules/{Subdomain}`, host composition, and cross-module contracts.
