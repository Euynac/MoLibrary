---
name: monica-project-unit-development
description: Shared Monica-native DDD ProjectUnit development guidance for business projects. Use when creating, naming, placing, or refactoring ApplicationService, RequestDto, DomainService, Entity, Repository, DomainEvent, DomainEventHandler, LocalEventHandler, Configuration, RecurringJob, or TriggeredJob types, or when deciding which ProjectUnits a new feature requires in either microservice or modular monolith solutions.
---

# Monica Project Unit Development

## Overview

Use this skill for unit-level business development in Monica-based DDD projects. It defines the common ProjectUnit language shared by both `monica-business-microservice` and `monica-business-modular-monolith`.

## Workflow

1. Start with the architecture skill to choose the target subdomain, project, and folder layout.
2. Read [00-project-unit-overview.md](references/00-project-unit-overview.md) and [02-project-unit-composition-map.md](references/02-project-unit-composition-map.md) to identify the units the feature needs.
3. Load only the template references relevant to the units you are creating or changing.
4. Keep the boundary thin: `ApplicationService` returns `Res`, while internal `DomainService`, repository, and entity logic stay on normal .NET return types and exceptions.
5. Prefer rich entities and value objects over procedural handlers that directly mutate persistence state.

## Ground Rules

- Use Monica-native base classes and interfaces only. Do not introduce `Our*` wrappers or FIPS-specific conventions.
- Follow the naming, placement, and boundary rules in [01-project-unit-naming-and-boundaries.md](references/01-project-unit-naming-and-boundaries.md). These rules are aligned with the current `Monica.Framework/ProjectUnits` discovery behavior.
- Keep persistence concerns in repositories and persistence classes, not in request handlers.
- Keep contracts stable: requests, DTOs, and events are not persistence entities.
- If a handler returns `Res<string>`, use `Res.Ok<string>(value)` instead of `Res.Ok(value)` to avoid the non-generic string overload.

## Reference Navigation

- Unit catalog and responsibilities: [00-project-unit-overview.md](references/00-project-unit-overview.md)
- Naming, placement, and boundary rules: [01-project-unit-naming-and-boundaries.md](references/01-project-unit-naming-and-boundaries.md)
- Unit selection by feature shape: [02-project-unit-composition-map.md](references/02-project-unit-composition-map.md)
- `ApplicationService` templates: [10-application-service-template.md](references/10-application-service-template.md)
- `DomainService` templates: [11-domain-service-template.md](references/11-domain-service-template.md)
- Entity, request, and event templates: [12-entity-request-event-template.md](references/12-entity-request-event-template.md)
- Repository and persistence templates: [13-repository-and-persistence-template.md](references/13-repository-and-persistence-template.md)
- Event handler templates: [14-event-handler-template.md](references/14-event-handler-template.md)
- Job templates: [15-job-template.md](references/15-job-template.md)
- Configuration templates: [16-configuration-template.md](references/16-configuration-template.md)
- Completion checklist: [17-feature-checklist.md](references/17-feature-checklist.md)

## Scope Notes

- This skill is architecture-agnostic. It explains unit design, not solution topology.
- Use `monica-business-microservice` to decide service splits, published-language layout, and migration project boundaries.
- Use `monica-business-modular-monolith` to decide module splits, host composition, and cross-module contracts.
