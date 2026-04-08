---
name: monica-business-modular-monolith
description: DDD modular monolith architecture guidance for Monica business projects. Use when creating or extending a bounded context under Domains, planning shared platform layers, shaping Platform.Protocol/PublishedLanguages, defining Application, Domain, Infrastructure, and host composition, or deciding collaboration boundaries inside a single deployment. Pair with monica-project-unit-development for unit-level implementation.
---

# Monica Business Modular Monolith

## Overview

Use this skill to structure a Monica business solution as a modular monolith with explicit subdomain boundaries. It keeps the deployment single while preserving contract-first collaboration and clear ownership per bounded context.

## Workflow

1. Read [00-solution-layout.md](references/00-solution-layout.md) to understand the target domain-first structure.
2. Use [01-create-subdomain-and-domain.md](references/01-create-subdomain-and-domain.md) when adding a new bounded context.
3. Use [02-protocol-platform-and-internal-collaboration.md](references/02-protocol-platform-and-internal-collaboration.md) before introducing cross-domain requests, DTOs, or events.
4. Use [03-boundaries-dependencies-and-persistence.md](references/03-boundaries-dependencies-and-persistence.md) to keep host composition and persistence ownership coherent.
5. Hand unit implementation to `monica-project-unit-development`, then finish with [04-delivery-checklist.md](references/04-delivery-checklist.md).

## Core Rules

- Split the solution by bounded context under `Domains/`, not by global `Application`, `Domain`, or `Infrastructure` buckets.
- Keep the shared platform split explicit: `Platform.BuildingBlocks` for project-agnostic infrastructure extensions, `Platform.Infrastructure` for solution-owned infrastructure wiring, and `Platform.Protocol` for shared business language.
- Keep cross-domain dependencies pointed at `Shared/Platform.Protocol/PublishedLanguages` and optional `AppInterfaces`. Do not reference another domain's `Application` or `Infrastructure` project directly.
- Keep AppHost entry projects thin. Composition happens there, but business logic stays inside the owning domain ProjectUnits.
- Treat the modular monolith as one deployment, not as hidden microservices in the same repository.

## Reference Navigation

- Solution structure: [00-solution-layout.md](references/00-solution-layout.md)
- New subdomain/domain workflow: [01-create-subdomain-and-domain.md](references/01-create-subdomain-and-domain.md)
- Protocol platform and collaboration: [02-protocol-platform-and-internal-collaboration.md](references/02-protocol-platform-and-internal-collaboration.md)
- Boundaries, dependencies, and persistence: [03-boundaries-dependencies-and-persistence.md](references/03-boundaries-dependencies-and-persistence.md)
- Completion checklist: [04-delivery-checklist.md](references/04-delivery-checklist.md)

## Scope Notes

- This skill defines domain boundaries and composition rules inside one deployment.
- Use `monica-project-unit-development` whenever the next step is to implement requests, handlers, entities, repositories, events, configurations, or jobs.
