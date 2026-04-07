# ProjectUnit Composition Map

Use this file to choose the smallest correct set of ProjectUnits for a feature.

## 1. New query use case

Create:

- `RequestDto`
- `ApplicationService`
- Optional `Repository` method for non-trivial querying
- Optional `DomainService` if the read rule is reused or computed

Load:

- [10-application-service-template.md](10-application-service-template.md)
- [12-entity-request-event-template.md](12-entity-request-event-template.md)
- [13-repository-and-persistence-template.md](13-repository-and-persistence-template.md) when persistence changes are needed

## 2. New command use case

Create:

- `RequestDto`
- `ApplicationService`
- Usually `DomainService`
- Update or add `Entity`
- Optional `DomainEvent`

Load:

- [10-application-service-template.md](10-application-service-template.md)
- [11-domain-service-template.md](11-domain-service-template.md)
- [12-entity-request-event-template.md](12-entity-request-event-template.md)

## 3. New aggregate or persistent entity

Create:

- `Entity`
- `Repository` interface and implementation
- Persistence mapping or `DbContext` update
- Optional `ApplicationService` if the aggregate also gets a new use case

Load:

- [12-entity-request-event-template.md](12-entity-request-event-template.md)
- [13-repository-and-persistence-template.md](13-repository-and-persistence-template.md)

## 4. New event-driven reaction

Create:

- `DomainEvent`
- `DomainEventHandler` or `LocalEventHandler`
- Optional `DomainService` if the reaction contains reusable business logic
- Optional `TriggeredJob` if the work should be asynchronous and decoupled

Load:

- [12-entity-request-event-template.md](12-entity-request-event-template.md)
- [14-event-handler-template.md](14-event-handler-template.md)
- [15-job-template.md](15-job-template.md) when the reaction becomes queued or delayed

## 5. New scheduled or asynchronous workflow

Create:

- `RecurringJob` for cron-like scheduled work
- `TriggeredJob` for on-demand background work
- Usually a `DomainService` to hold the actual business operation
- Optional `Configuration` for schedule or behavior tuning

Load:

- [11-domain-service-template.md](11-domain-service-template.md)
- [15-job-template.md](15-job-template.md)
- [16-configuration-template.md](16-configuration-template.md)

## 6. New runtime option

Create:

- `Configuration`
- Consumers that inject `IOptions<T>`, `IOptionsSnapshot<T>`, or `IOptionsMonitor<T>`

Load:

- [16-configuration-template.md](16-configuration-template.md)
