---
name: monica-business-microservice
description: DDD microservice architecture guidance for Monica business projects. Use when creating or extending a subdomain service, planning the strict solution-project dependency chain across Platform layers, placing project-common versus service-only library references, shaping Platform.Protocol/PublishedLanguages, splitting API, Domain, and migration projects, or deciding cross-service collaboration boundaries. Pair with monica-project-unit-development for unit-level implementation.
---

# Monica Business Microservice

## Overview

Use this skill to shape Monica business projects as DDD-aligned microservices. It defines where each subdomain lives, how shared contracts are exposed, and how services collaborate without collapsing boundaries.

## Workflow

1. Read [00-solution-layout.md](references/00-solution-layout.md) to choose the target solution structure.
2. Use [01-create-subdomain-and-service.md](references/01-create-subdomain-and-service.md) when adding a new subdomain or service trio.
3. Use [02-protocol-platform-and-service-contracts.md](references/02-protocol-platform-and-service-contracts.md) before adding cross-service requests, DTOs, or events.
4. Use [03-service-boundaries-and-collaboration.md](references/03-service-boundaries-and-collaboration.md) to keep service ownership and dependency direction clean.
5. Hand unit implementation to `monica-project-unit-development`, then finish with [04-delivery-checklist.md](references/04-delivery-checklist.md).

## Core Rules

- Split by business capability and data ownership, not by transport or technical layer alone.
- Keep the shared platform split explicit: `Platform.BuildingBlocks` for project-agnostic infrastructure extensions, `Platform.Infrastructure` for solution-owned infrastructure wiring, and `Platform.Protocol` for shared business language.
- Use the strict solution-project reference chain `{Subdomain}Service.API -> {Subdomain}Service.Domain -> Platform.Infrastructure -> Platform.Protocol -> Platform.BuildingBlocks`.
- Put project-common library references in `Platform.BuildingBlocks`. Keep service-only package references in the owning `{Subdomain}Service.Domain` project.
- Keep `Shared/Platform.Protocol/PublishedLanguages` stable and explicit. Do not leak persistence entities across service boundaries.
- Keep each subdomain service independently evolvable: `API`, `Domain`, and migrations move together.
- Keep pure helper code in the service `Domain` project under `Utilities/`, using `Utils*` names.
- Treat the service `API` project and AppHost or gateway entry points as adapters or composition only. Keep AppHost or gateway projects down to the project file and `Program.cs`; business ProjectUnits belong in the service's `API` and `Domain` projects.
- Keep `.slnx` solution folders aligned with the physical layout under `src/AppHost`, `src/Shared`, `src/Services`, and `src/Migrations`.

## Reference Navigation

- Solution structure: [00-solution-layout.md](references/00-solution-layout.md)
- New subdomain/service workflow: [01-create-subdomain-and-service.md](references/01-create-subdomain-and-service.md)
- Protocol platform and service contracts: [02-protocol-platform-and-service-contracts.md](references/02-protocol-platform-and-service-contracts.md)
- Service boundaries and collaboration: [03-service-boundaries-and-collaboration.md](references/03-service-boundaries-and-collaboration.md)
- Completion checklist: [04-delivery-checklist.md](references/04-delivery-checklist.md)

## Scope Notes

- This skill defines solution layout and service-level boundaries.
- Use `monica-project-unit-development` whenever the next step is to implement requests, handlers, entities, repositories, events, configurations, or jobs.
