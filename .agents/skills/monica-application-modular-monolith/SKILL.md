---
name: monica-application-modular-monolith
description: DDD modular monolith architecture guidance for Monica application projects. Use when creating or extending a bounded context under Domains, planning the strict solution-project dependency chain across Platform layers, placing project-common versus subdomain-only library references, shaping Platform.Protocol/PublishedLanguages, defining domain-owned Application layout, merged domain packages, and host composition, or deciding collaboration boundaries inside a single deployment. Pair with monica-application-project-unit-development for unit-level implementation.
---

# Monica Application Modular Monolith

## Overview

Use this skill to structure a Monica application solution as a modular monolith with explicit subdomain boundaries. It keeps the deployment single while preserving contract-first collaboration and clear ownership per bounded context.

## Workflow

1. Read [00-solution-layout.md](references/00-solution-layout.md) to understand the target domain-first structure.
2. Use [01-create-subdomain-and-domain.md](references/01-create-subdomain-and-domain.md) when adding a new bounded context.
3. Use [02-protocol-platform-and-internal-collaboration.md](references/02-protocol-platform-and-internal-collaboration.md) before introducing cross-domain requests, DTOs, or events.
4. Use [03-boundaries-dependencies-and-persistence.md](references/03-boundaries-dependencies-and-persistence.md) to keep host composition and persistence ownership coherent.
5. Hand unit implementation to `monica-application-project-unit-development`, then finish with [04-delivery-checklist.md](references/04-delivery-checklist.md).

## Core Rules

- Split the solution by bounded context under `Domains/`, not by global `Application`, `Domain`, or `Infrastructure` buckets.
- Each bounded context under `Domains/` uses a single `Domains.{Subdomain}.csproj` that keeps `Application`, domain models, and domain-owned infrastructure together.
- Keep the shared platform split explicit: `Platform.BuildingBlocks` for project-agnostic infrastructure extensions, `Platform.Infrastructure` for solution-owned infrastructure wiring, and `Platform.Protocol` for shared business language.
- Use the strict solution-project reference chain `AppHost -> Domains.{Subdomain} -> Platform.Infrastructure -> Platform.Protocol -> Platform.BuildingBlocks`.
- Put project-common library references in `Platform.BuildingBlocks`. Keep subdomain-only package references in the owning `Domains.{Subdomain}.csproj`.
- Keep cross-domain dependencies pointed at `Shared/Platform.Protocol/PublishedLanguages`, consuming shared `Contracts/` and only the deliberate `Implementations/*` surfaces exposed there. Do not reference another domain's internal implementation directly.
- Keep domain-owned application units in `Application/HandlersCommand`, `Application/HandlersQuery`, `Application/HandlersEvent`, and `Application/BackgroundWorkers`.
- Keep repository implementations and `DbContext`-related files in `Repository/`, and keep pure helper code in `Utilities/` with `Utils*` names.
- Declare every `ApplicationService` endpoint on its `Command*` or `Query*` request with `[ApiEndpoint]`; handlers contain behavior, not MVC route or binding attributes.
- A request in strict `Platform.Protocol.PublishedLanguages.Domain{Subdomain}.Requests` with `[ApiEndpoint]` is published HTTP/RPC language and generates `I{Subdomain}CommandApi` or `I{Subdomain}QueryApi`. A request local to the domain project generates HTTP only.
- Keep one `WebApiGenerationConfig("api/v1", DomainName = "{Subdomain}")` in each domain assembly for local endpoints, and configure published RPC targets in `Platform.Protocol`. Do not use RPC metadata snapshots or source-tree export tasks.
- AppHost entry projects are composition-only entry points. Keep them down to the project file and `Program.cs`; do not place business ProjectUnits there.
- Register `monica.AddConfiguration()` inside the AppHost's single `builder.AddMonica(...)` callback. Read bootstrap values from `builder.Configuration` during composition; consume Configuration ProjectUnits through typed options injection at runtime.
- Keep `.slnx` solution folders aligned with the physical layout under `src/AppHost`, `src/Shared`, and `src/Domains`.
- Treat the modular monolith as one deployment, not as hidden microservices in the same repository.

## Reference Navigation

- Solution structure: [00-solution-layout.md](references/00-solution-layout.md)
- New subdomain/domain workflow: [01-create-subdomain-and-domain.md](references/01-create-subdomain-and-domain.md)
- Protocol platform and collaboration: [02-protocol-platform-and-internal-collaboration.md](references/02-protocol-platform-and-internal-collaboration.md)
- Boundaries, dependencies, and persistence: [03-boundaries-dependencies-and-persistence.md](references/03-boundaries-dependencies-and-persistence.md)
- Completion checklist: [04-delivery-checklist.md](references/04-delivery-checklist.md)

## Scope Notes

- This skill defines domain boundaries and composition rules inside one deployment.
- Use `monica-application-project-unit-development` whenever the next step is to implement requests, handlers, entities, repositories, events, configurations, or jobs.
