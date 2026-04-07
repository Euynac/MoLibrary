# ProjectUnit Naming and Boundaries

Use these rules to keep Monica project-unit discovery, code navigation, and DDD boundaries aligned.

## Naming Rules

| Unit | Required or recommended pattern |
| --- | --- |
| `ApplicationService` | Include `Handler` in the class name. Prefer `CommandHandler*` or `QueryHandler*`. |
| `DomainService` | Start the class name with `Domain`. |
| `Repository` | Name the interface `IRepository*` and the implementation `Repository*`. |
| `DomainEvent` | Start the event type with `Event`. |
| `DomainEventHandler` | Start the handler type with `DomainEventHandler`. |
| `LocalEventHandler` | Start the handler type with `LocalEventHandler`. |
| `Entity` | Place the type under `Entities/`. |
| `Configuration` | End the type name with `Options`. |
| `RecurringJob` | Start the class name with `Worker`. |
| `TriggeredJob` | Start the class name with `Job`. |

## Boundary Rules

- Keep `Res` and `Res<T>` at the `ApplicationService` boundary. Domain services and repositories should prefer normal return types and exceptions.
- Keep orchestration in `ApplicationService`, not business rules. If a handler becomes hard to explain in one paragraph, move the reusable rule into a `DomainService` or the entity itself.
- Keep entity invariants on the entity. Do not let handlers or repositories directly toggle domain state through scattered property assignments.
- Keep repository interfaces on the domain side of the boundary and repository implementations on the infrastructure side.
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
